#!/usr/bin/env bash
# Toolchain for Claude Code cloud sessions (Ubuntu 24.04, x64): .NET 10 SDK, Blender 5.2.2 LTS with the
# MCP for Blender add-on, and the Unity 6000.3.26f1 editor; activates Unity when a license is configured.
# Idempotent: safe to run at the start of every session. Paste it into the cloud environment's setup script.
#
# Unity license (environment variables, never committed): UNITY_EMAIL and UNITY_PASSWORD, plus either
# UNITY_SERIAL (Plus/Pro) or UNITY_LICENSE (Personal: the contents of Unity_lic.ulf from a machine where
# Unity Hub is signed in). Without them, Unity is installed but stays unlicensed.
set -euo pipefail

DOTNET_DIR="${DOTNET_DIR:-$HOME/.dotnet}"
BLENDER_VERSION=5.2.2
BLENDER_DIR="/opt/blender/blender-${BLENDER_VERSION}-linux-x64"
UNITY_VERSION=6000.3.26f1
UNITY_CHANGESET=de8167e7440c
UNITY_DIR="/opt/unity/${UNITY_VERSION}"
MCP_FOR_BLENDER=mcp-for-blender@2.1.9

log() { printf '[setup_env] %s\n' "$*"; }

if [ ! -x "$DOTNET_DIR/dotnet" ] || ! "$DOTNET_DIR/dotnet" --list-sdks | grep -q '^10\.'; then
  log "installing .NET 10 SDK"
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 --install-dir "$DOTNET_DIR"
fi

if [ ! -x "$BLENDER_DIR/blender" ]; then
  log "installing Blender $BLENDER_VERSION"
  mkdir -p /opt/blender
  curl -sSL --retry 4 "https://download.blender.org/release/Blender${BLENDER_VERSION%.*}/blender-${BLENDER_VERSION}-linux-x64.tar.xz" \
    | tar -xJ -C /opt/blender
fi
ln -sf "$BLENDER_DIR/blender" /usr/local/bin/blender

ADDONS_DIR="$HOME/.config/blender/${BLENDER_VERSION%.*}/scripts/addons"
if [ ! -f "$ADDONS_DIR/blender_mcp.py" ] && command -v uvx >/dev/null; then
  log "installing the MCP for Blender add-on"
  mkdir -p "$ADDONS_DIR"
  BLENDERMCP_ADDONS_DIR="$ADDONS_DIR" DISABLE_TELEMETRY=true uvx "$MCP_FOR_BLENDER" install-addon
fi

if [ ! -x "$UNITY_DIR/Editor/Unity" ]; then
  log "installing Unity $UNITY_VERSION (4.5 GB download, 8.2 GB on disk)"
  mkdir -p "$UNITY_DIR"
  curl -sSL --retry 4 "https://download.unity3d.com/download_unity/${UNITY_CHANGESET}/LinuxEditorInstaller/Unity-${UNITY_VERSION}.tar.xz" \
    | tar -xJ -C "$UNITY_DIR"
fi
ln -sf "$UNITY_DIR/Editor/Unity" /usr/local/bin/unity-editor

if [ -n "${UNITY_EMAIL:-}" ] && [ -n "${UNITY_PASSWORD:-}" ]; then
  if [ -n "${UNITY_SERIAL:-}" ]; then
    log "activating Unity with a serial"
    unity-editor -batchmode -nographics -quit -serial "$UNITY_SERIAL" \
      -username "$UNITY_EMAIL" -password "$UNITY_PASSWORD" -logFile - | grep -iE 'licen|error' || true
  elif [ -n "${UNITY_LICENSE:-}" ]; then
    log "activating Unity with a license file"
    license_file="$(mktemp --suffix .ulf)"
    printf '%s' "$UNITY_LICENSE" > "$license_file"
    unity-editor -batchmode -nographics -quit -manualLicenseFile "$license_file" -logFile - | grep -iE 'licen|error' || true
    rm -f "$license_file"
  fi
else
  log "no Unity credentials in the environment; Unity stays unlicensed"
fi
log "done"
