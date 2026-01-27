#!/usr/bin/env python3
"""
MTG Fire Detection Parser - GLOBAL VERSION
Accepts dynamic bounding box for any region worldwide
"""

import netCDF4 as nc
import numpy as np
from pyproj import CRS, Transformer
import json
import sys

def parse_mtg_fire(nc_file, bbox_str=None):
    """
    Parse MTG NetCDF file for fire detections
    
    Args:
        nc_file: Path to NetCDF file
        bbox_str: Bounding box as "min_lat,min_lon,max_lat,max_lon"
                  If None, returns ALL fires globally
    
    Returns:
        JSON with metadata and fire detections
    """
    
    # Parse bbox if provided
    # C# kodu Eumetsat formatı gönderir: minLon,minLat,maxLon,maxLat
    if bbox_str:
        try:
            parts = bbox_str.split(',')
            min_lon = float(parts[0])  # Türkiye: ~26
            min_lat = float(parts[1])  # Türkiye: ~36
            max_lon = float(parts[2])  # Türkiye: ~45
            max_lat = float(parts[3])  # Türkiye: ~42
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
    
    all_fires = []
    filtered_fires = []
    
    # Find fire pixels (LOW=1, MEDIUM=2, HIGH=3)
    fire_mask = (fire_result >= 1) & (fire_result <= 3)
    rows, cols = np.where(fire_mask)
    
    total_detections = len(rows)
    print(f"DEBUG: Found {total_detections} fire detections (all confidence levels)", 
          file=sys.stderr)
    
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
                continue
            
            # Get probability
            prob_raw = fire_probability[row, col]
            if hasattr(prob_raw, "mask") and prob_raw.mask:
                prob_val = None
            else:
                prob_val = None if prob_raw == -127 else round(float(prob_raw) * 0.01, 3)
            
            # Get confidence
            c_val = int(fire_result[row, col])
            
            fire_info = {
                "latitude": round(float(lat), 6),
                "longitude": round(float(lon), 6),
                "confidence": confidence_map.get(c_val, "unknown"),
                "confidence_value": c_val,
                "probability": prob_val
            }
            
            all_fires.append(fire_info)
            
            # Apply bbox filter if specified
            if use_bbox:
                in_lat = (min_lat <= lat <= max_lat)
                in_lon = (min_lon <= lon <= max_lon)
                in_region = in_lat and in_lon
                
                if in_region:
                    filtered_fires.append(fire_info)
                    
                    # Debug first 20 matches
                    if len(filtered_fires) <= 20:
                        print(f"✅ MATCH #{len(filtered_fires)}: "
                              f"lat={lat:.4f}, lon={lon:.4f}, "
                              f"conf={confidence_map.get(c_val)}", 
                              file=sys.stderr)
            else:
                # No filtering - add all fires
                filtered_fires.append(fire_info)
        
        except Exception as e:
            print(f"ERROR processing pixel {i}: {e}", file=sys.stderr)
            continue
    
    ds.close()
    
    # Summary
    if use_bbox:
        print(f"DEBUG: Total detections={len(all_fires)}, "
              f"In region={len(filtered_fires)}", 
              file=sys.stderr)
    else:
        print(f"DEBUG: Global detections={len(filtered_fires)}", 
              file=sys.stderr)
    
    # Build result
    result = {
        "metadata": {
            "time_start": time_start_iso,
            "time_end": time_end_iso,
            "satellite": "MTG-I1",
            "instrument": "FCI",
            "product": "FIR - Fire Detection",
            "total_detections": len(all_fires),
            "filtered_detections": len(filtered_fires),
            "bbox_applied": use_bbox
        },
        "fires": filtered_fires
    }
    
    # Add bbox to metadata if used
    if use_bbox:
        result["metadata"]["bbox"] = {
            "min_lat": min_lat,
            "min_lon": min_lon,
            "max_lat": max_lat,
            "max_lon": max_lon
        }
    
    return result


if __name__ == "__main__":
    # Usage: python3 parse_mtg_fire.py <nc_file> [bbox]
    # bbox formatı: minLon,minLat,maxLon,maxLat (Eumetsat formatı)
    # Example: python3 parse_mtg_fire.py file.nc "26,36,45,42"  # Türkiye
    
    if len(sys.argv) < 2:
        print(json.dumps({
            "error": "Usage: parse_mtg_fire.py <nc_file> [bbox]",
            "example": "parse_mtg_fire.py file.nc '35.8,25.7,42.2,45'"
        }))
        sys.exit(1)
    
    nc_file = sys.argv[1]
    bbox_str = sys.argv[2] if len(sys.argv) > 2 else None
    
    try:
        result = parse_mtg_fire(nc_file, bbox_str)
        print(json.dumps(result))
    except Exception as e:
        print(json.dumps({"error": str(e)}), file=sys.stderr)
        sys.exit(1)