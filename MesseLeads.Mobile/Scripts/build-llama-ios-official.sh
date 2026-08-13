#!/usr/bin/env bash
set -Eeuo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR="$ROOT_DIR/.native-build-official"
LLAMA_DIR="$WORK_DIR/llama.cpp"
LOG_FILE="$ROOT_DIR/build-llama-ios-official.log"
OUTPUT_DIR="$ROOT_DIR/Platforms/iOS/Native"
LLAMA_OUTPUT="$OUTPUT_DIR/llama.xcframework"
BRIDGE_OUTPUT="$OUTPUT_DIR/MesseLeadsLlamaBridge.xcframework"
BRIDGE_SOURCE="$ROOT_DIR/Native/iOS/messeleads_llama_bridge.cpp"
BRIDGE_HEADER="$ROOT_DIR/Native/iOS/messeleads_llama_bridge.h"

# Gepinnte Version. Bridge und llama.cpp werden immer gemeinsam gegen diesen Stand gebaut.
LLAMA_REF="b10052"
IOS_MIN_VERSION="15.0"

on_error() {
    local exit_code=$?
    echo
    echo "FEHLER: Der native iOS-Build wurde abgebrochen (Exitcode $exit_code)."
    echo "Die letzten 80 Zeilen stehen hier:"
    echo "  $LOG_FILE"
    if [[ -f "$LOG_FILE" ]]; then
        tail -80 "$LOG_FILE" || true
    fi
    exit "$exit_code"
}
trap on_error ERR

exec > >(tee "$LOG_FILE") 2>&1

require_tool() {
    local tool="$1"
    if ! command -v "$tool" >/dev/null 2>&1; then
        echo "Fehlendes Werkzeug: $tool"
        exit 1
    fi
}

require_tool git
require_tool cmake
require_tool xcrun
require_tool xcodebuild
require_tool clang++
require_tool nm
require_tool sed

if [[ ! -f "$BRIDGE_SOURCE" || ! -f "$BRIDGE_HEADER" ]]; then
    echo "Bridge-Quellen fehlen unter Native/iOS."
    exit 1
fi

echo "============================================================"
echo "MesseLeads: offizieller llama.cpp Apple-Build"
echo "Projekt:       $ROOT_DIR"
echo "llama.cpp:     $LLAMA_REF"
echo "iOS Minimum:   $IOS_MIN_VERSION"
echo "Logdatei:      $LOG_FILE"
echo "============================================================"

rm -rf "$WORK_DIR"
mkdir -p "$WORK_DIR" "$OUTPUT_DIR"

# Alte, unvollstaendige Frameworks entfernen.
rm -rf "$LLAMA_OUTPUT" "$BRIDGE_OUTPUT"

echo
echo "[1/6] llama.cpp ($LLAMA_REF) klonen ..."
git clone --depth 1 --branch "$LLAMA_REF" \
    https://github.com/ggml-org/llama.cpp.git \
    "$LLAMA_DIR"

cd "$LLAMA_DIR"

if [[ ! -f build-xcframework.sh ]]; then
    echo "Das offizielle build-xcframework.sh wurde nicht gefunden."
    exit 1
fi

# Das offizielle Skript verwenden, aber den Deployment-Target an die MAUI-App anpassen.
if grep -q '^IOS_MIN_OS_VERSION=' build-xcframework.sh; then
    sed -i '' "s/^IOS_MIN_OS_VERSION=.*/IOS_MIN_OS_VERSION=$IOS_MIN_VERSION/" build-xcframework.sh
else
    echo "IOS_MIN_OS_VERSION konnte im offiziellen Skript nicht gesetzt werden."
    exit 1
fi

chmod +x build-xcframework.sh

echo
echo "[2/6] Offizielles build-xcframework.sh ausfuehren ..."
echo "Dieser Schritt kann deutlich laenger dauern."
./build-xcframework.sh

OFFICIAL_XCFRAMEWORK="$LLAMA_DIR/build-apple/llama.xcframework"
if [[ ! -d "$OFFICIAL_XCFRAMEWORK" ]]; then
    echo "Das offizielle Ergebnis wurde nicht erzeugt:"
    echo "  $OFFICIAL_XCFRAMEWORK"
    exit 1
fi

echo
echo "[3/6] Offizielles llama.xcframework ins MAUI-Projekt kopieren ..."
cp -R "$OFFICIAL_XCFRAMEWORK" "$LLAMA_OUTPUT"

DEVICE_FRAMEWORK="$(find "$LLAMA_OUTPUT" -type d -path '*/ios-arm64/llama.framework' -print -quit)"
SIM_FRAMEWORK="$(find "$LLAMA_OUTPUT" -type d -path '*simulator*/llama.framework' -print -quit)"

if [[ -z "$DEVICE_FRAMEWORK" || -z "$SIM_FRAMEWORK" ]]; then
    echo "iOS-Device- oder Simulator-Slice im llama.xcframework fehlt."
    find "$LLAMA_OUTPUT" -maxdepth 3 -type d
    exit 1
fi

DEVICE_HEADERS="$DEVICE_FRAMEWORK/Headers"
SIM_HEADERS="$SIM_FRAMEWORK/Headers"

if [[ ! -f "$DEVICE_HEADERS/llama.h" || ! -f "$DEVICE_HEADERS/ggml-backend.h" ]]; then
    echo "Benötigte llama.cpp-Header fehlen im Device-Slice."
    exit 1
fi

BRIDGE_BUILD="$WORK_DIR/bridge"
mkdir -p "$BRIDGE_BUILD/device" "$BRIDGE_BUILD/simulator" "$BRIDGE_BUILD/headers"
cp "$BRIDGE_HEADER" "$BRIDGE_BUILD/headers/"

DEVICE_SDK="$(xcrun --sdk iphoneos --show-sdk-path)"
SIM_SDK="$(xcrun --sdk iphonesimulator --show-sdk-path)"

echo
echo "[4/6] MesseLeads-Bridge fuer echtes iPhone/iPad bauen ..."
xcrun --sdk iphoneos clang++ \
    -std=c++17 \
    -arch arm64 \
    -isysroot "$DEVICE_SDK" \
    -miphoneos-version-min="$IOS_MIN_VERSION" \
    -I"$DEVICE_HEADERS" \
    -c "$BRIDGE_SOURCE" \
    -o "$BRIDGE_BUILD/device/messeleads_llama_bridge.o"

xcrun libtool -static \
    -o "$BRIDGE_BUILD/device/libMesseLeadsLlamaBridge.a" \
    "$BRIDGE_BUILD/device/messeleads_llama_bridge.o"

echo
echo "[5/6] MesseLeads-Bridge fuer Apple-Silicon-Simulator bauen ..."
xcrun --sdk iphonesimulator clang++ \
    -std=c++17 \
    -arch arm64 \
    -isysroot "$SIM_SDK" \
    -mios-simulator-version-min="$IOS_MIN_VERSION" \
    -I"$SIM_HEADERS" \
    -c "$BRIDGE_SOURCE" \
    -o "$BRIDGE_BUILD/simulator/messeleads_llama_bridge.o"

xcrun libtool -static \
    -o "$BRIDGE_BUILD/simulator/libMesseLeadsLlamaBridge.a" \
    "$BRIDGE_BUILD/simulator/messeleads_llama_bridge.o"

echo
echo "[6/6] Bridge-XCFramework erzeugen und Symbole pruefen ..."
xcodebuild -create-xcframework \
    -library "$BRIDGE_BUILD/device/libMesseLeadsLlamaBridge.a" \
    -headers "$BRIDGE_BUILD/headers" \
    -library "$BRIDGE_BUILD/simulator/libMesseLeadsLlamaBridge.a" \
    -headers "$BRIDGE_BUILD/headers" \
    -output "$BRIDGE_OUTPUT"

DEVICE_BRIDGE_LIB="$(find "$BRIDGE_OUTPUT" -type f -name 'libMesseLeadsLlamaBridge.a' -path '*/ios-arm64/*' -print -quit)"
SIM_BRIDGE_LIB="$(find "$BRIDGE_OUTPUT" -type f -name 'libMesseLeadsLlamaBridge.a' -path '*simulator*' -print -quit)"

if [[ -z "$DEVICE_BRIDGE_LIB" || -z "$SIM_BRIDGE_LIB" ]]; then
    echo "Die erzeugten Bridge-Bibliotheken konnten nicht gefunden werden."
    exit 1
fi

for lib in "$DEVICE_BRIDGE_LIB" "$SIM_BRIDGE_LIB"; do
    nm -gU "$lib" | grep -q '_ml_llama_generate' || {
        echo "Export ml_llama_generate fehlt in $lib"
        exit 1
    }
    nm -gU "$lib" | grep -q '_ml_llama_get_last_error' || {
        echo "Export ml_llama_get_last_error fehlt in $lib"
        exit 1
    }
done

echo
echo "============================================================"
echo "ERFOLG"
echo "Erzeugt wurden:"
echo "  $LLAMA_OUTPUT"
echo "  $BRIDGE_OUTPUT"
echo
echo "Die beiden kompletten XCFramework-Ordner jetzt nach Windows"
echo "in Platforms/iOS/Native kopieren."
echo "============================================================"
