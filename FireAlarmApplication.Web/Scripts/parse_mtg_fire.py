#!/usr/bin/env python3
"""
MTG Fire Detection Parser - GLOBAL VERSION
Accepts dynamic bounding box for any region worldwide

Filters:
  1. Land/Sea mask  → Deniz üzerindeki termal anomalileri eler
  2. Confidence      → Minimum confidence seviyesi filtresi
  3. Bounding box    → Belirli bölge ile sınırlama
"""

import netCDF4 as nc
import numpy as np
from pyproj import CRS, Transformer
from global_land_mask import globe
import json
import sys


def parse_mtg_fire(nc_file, bbox_str=None, min_confidence=1):
    """
    Parse MTG NetCDF file for fire detections

    Args:
        nc_file: Path to NetCDF file
        bbox_str: Bounding box as "minLon,minLat,maxLon,maxLat" (Eumetsat format)
                  If None, returns ALL fires globally
        min_confidence: Minimum confidence level (1=low, 2=medium, 3=high)

    Returns:
        JSON with metadata and fire detections
    """

    # Parse bbox if provided
    # C# kodu Eumetsat formatı gönderir: minLon,minLat,maxLon,maxLat
    if bbox_str:
        try:
            parts = bbox_str.split(',')
            min_lon = float(parts[0])
            min_lat = float(parts[1])
            max_lon = float(parts[2])
            max_lat = float(parts[3])
            use_bbox = True
            print(f"DEBUG: Using bbox = lat[{min_lat}, {max_lat}], lon[{min_lon}, {max_lon}]",
                  file=sys.stderr)
        except:
            print(f"WARNING: Invalid bbox format, using global mode", file=sys.stderr)
            use_bbox = False
    else:
        use_bbox = False
        print("DEBUG: Global mode - no bbox filtering", file=sys.stderr)

    # Open NetCDF file
    ds = nc.Dataset(nc_file, 'r')

    # Read variables
    x = ds.variables["x"][:]
    y = ds.variables["y"][:]
    fire_result = ds.variables["fire_result"][:]
    fire_probability = ds.variables["fire_probability"][:]

    # Get projection parameters
    proj = ds.variables["mtg_geos_projection"]
    h = proj.perspective_point_height
    a = proj.semi_major_axis
    b = proj.semi_minor_axis
    lon_0 = proj.longitude_of_projection_origin

    # Get time info
    time_start = ds.time_coverage_start
    time_end = ds.time_coverage_end

    def to_iso(t):
        """Convert timestamp to ISO format"""
        return (f"{t[:4]}-{t[4:6]}-{t[6:8]}"
                f"T{t[8:10]}:{t[10:12]}:{t[12:14]}Z")

    time_start_iso = to_iso(time_start)
    time_end_iso = to_iso(time_end)

    # Setup coordinate transformation
    crs_geos = CRS.from_proj4(
        f"+proj=geos +lon_0={lon_0} +h={h} +a={a} +b={b} +sweep=y"
    )
    crs_latlon = CRS.from_epsg(4326)
    transformer = Transformer.from_crs(crs_geos, crs_latlon, always_xy=True)

    # Confidence mapping
    confidence_map = {1: "low", 2: "medium", 3: "high"}

    # Counters for stats
    stats = {
        "total_raw": 0,
        "filtered_sea": 0,
        "filtered_low_confidence": 0,
        "filtered_outside_bbox": 0,
        "invalid_coords": 0,
        "accepted": 0
    }

    filtered_fires = []

    # Find fire pixels (LOW=1, MEDIUM=2, HIGH=3)
    fire_mask = (fire_result >= 1) & (fire_result <= 3)
    rows, cols = np.where(fire_mask)

    stats["total_raw"] = len(rows)
    print(f"DEBUG: Found {len(rows)} raw fire detections", file=sys.stderr)

    # Process each fire pixel
    for i, (row, col) in enumerate(zip(rows, cols)):
        try:
            # Convert pixel coordinates to lat/lon
            x_rad = x[col]
            y_rad = y[row]
            x_m = x_rad * h
            y_m = y_rad * h

            lon, lat = transformer.transform(x_m, y_m)

            # Skip invalid coordinates
            if np.isnan(lat) or np.isnan(lon):
                stats["invalid_coords"] += 1
                continue

            if lat < -90 or lat > 90 or lon < -180 or lon > 180:
                stats["invalid_coords"] += 1
                continue

            # Get confidence value
            c_val = int(fire_result[row, col])

            # --- FILTER 1: Confidence threshold ---
            if c_val < min_confidence:
                stats["filtered_low_confidence"] += 1
                continue

            # --- FILTER 2: Land/Sea mask ---
            # Deniz üzerindeki termal anomalileri (sun glint, sıcak akıntı, gemi) ele
            if not globe.is_land(lat, lon):
                stats["filtered_sea"] += 1
                continue

            # --- FILTER 3: Bounding box ---
            if use_bbox:
                if not (min_lat <= lat <= max_lat and min_lon <= lon <= max_lon):
                    stats["filtered_outside_bbox"] += 1
                    continue

            # Get probability
            prob_raw = fire_probability[row, col]
            if hasattr(prob_raw, "mask") and prob_raw.mask:
                prob_val = None
            else:
                prob_val = None if prob_raw == -127 else round(float(prob_raw) * 0.01, 3)

            fire_info = {
                "latitude": round(float(lat), 6),
                "longitude": round(float(lon), 6),
                "confidence": confidence_map.get(c_val, "unknown"),
                "confidence_value": c_val,
                "probability": prob_val
            }

            filtered_fires.append(fire_info)
            stats["accepted"] += 1

            # Debug first 10 accepted fires
            if stats["accepted"] <= 10:
                print(f"  FIRE #{stats['accepted']}: "
                      f"lat={lat:.4f}, lon={lon:.4f}, "
                      f"conf={confidence_map.get(c_val)}, prob={prob_val}",
                      file=sys.stderr)

        except Exception as e:
            print(f"ERROR processing pixel {i}: {e}", file=sys.stderr)
            continue

    ds.close()

    # Summary
    print(f"DEBUG: Filter results:", file=sys.stderr)
    print(f"  Raw detections:      {stats['total_raw']}", file=sys.stderr)
    print(f"  Invalid coords:      {stats['invalid_coords']}", file=sys.stderr)
    print(f"  Filtered (sea):      {stats['filtered_sea']}", file=sys.stderr)
    print(f"  Filtered (conf<{min_confidence}):   {stats['filtered_low_confidence']}", file=sys.stderr)
    print(f"  Filtered (bbox):     {stats['filtered_outside_bbox']}", file=sys.stderr)
    print(f"  ACCEPTED:            {stats['accepted']}", file=sys.stderr)

    # Build result
    result = {
        "metadata": {
            "time_start": time_start_iso,
            "time_end": time_end_iso,
            "satellite": "MTG-I1",
            "instrument": "FCI",
            "product": "FIR - Fire Detection",
            "total_raw_detections": stats["total_raw"],
            "filtered_detections": stats["accepted"],
            "filter_stats": stats,
            "bbox_applied": use_bbox,
            "min_confidence": min_confidence,
            "land_filter": True
        },
        "fires": filtered_fires
    }

    if use_bbox:
        result["metadata"]["bbox"] = {
            "min_lat": min_lat,
            "min_lon": min_lon,
            "max_lat": max_lat,
            "max_lon": max_lon
        }

    return result


if __name__ == "__main__":
    # Usage: python3 parse_mtg_fire.py <nc_file> [bbox] [min_confidence]
    # bbox format: minLon,minLat,maxLon,maxLat (Eumetsat format)
    # min_confidence: 1=low, 2=medium, 3=high (default: 2)
    #
    # Examples:
    #   python3 parse_mtg_fire.py file.nc "26,36,45,42"      # Turkey, conf >= 2
    #   python3 parse_mtg_fire.py file.nc "26,36,45,42" 1    # Turkey, all confidence
    #   python3 parse_mtg_fire.py file.nc                     # Global, conf >= 2

    if len(sys.argv) < 2:
        print(json.dumps({
            "error": "Usage: parse_mtg_fire.py <nc_file> [bbox] [min_confidence]",
            "example": "parse_mtg_fire.py file.nc '26,36,45,42' 2"
        }))
        sys.exit(1)

    nc_file = sys.argv[1]
    bbox_str = sys.argv[2] if len(sys.argv) > 2 else None
    min_confidence = int(sys.argv[3]) if len(sys.argv) > 3 else 2  # Default: medium+

    try:
        result = parse_mtg_fire(nc_file, bbox_str, min_confidence)
        print(json.dumps(result))
    except Exception as e:
        print(json.dumps({"error": str(e)}), file=sys.stderr)
        sys.exit(1)