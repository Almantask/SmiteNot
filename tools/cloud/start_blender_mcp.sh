#!/usr/bin/env bash
# Start Blender's UI on a virtual display (Xvfb) with the MCP for Blender add-on enabled, so the
# `blender` MCP server in .mcp.json can reach it on localhost:9876. Cloud sessions only; on a desktop,
# just open Blender. Usage: tools/cloud/start_blender_mcp.sh [file.blend]   (log: /tmp/blender_mcp.log)
set -euo pipefail
BLEND="${1:-}"
nohup xvfb-run -a -s "-screen 0 1600x900x24" blender ${BLEND:+"$BLEND"} \
  --python-expr "import bpy; bpy.ops.preferences.addon_enable(module='blender_mcp')" \
  > /tmp/blender_mcp.log 2>&1 &
for _ in $(seq 1 120); do
  if python3 -c "import socket; socket.create_connection(('localhost', 9876), 1)" 2>/dev/null; then
    echo "Blender MCP add-on listening on localhost:9876"
    exit 0
  fi
  sleep 1
done
echo "Blender didn't open port 9876 within 120 s; see /tmp/blender_mcp.log" >&2
exit 1
