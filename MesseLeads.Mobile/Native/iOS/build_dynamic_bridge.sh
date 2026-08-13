#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
LLAMA_XCFRAMEWORK="${1:-$PROJECT_ROOT/Platforms/iOS/Native/llama.xcframework}"
OUTPUT_XCFRAMEWORK="${2:-$PROJECT_ROOT/Platforms/iOS/Native/MesseLeadsLlamaBridge.xcframework}"
BUILD_DIR="$SCRIPT_DIR/build/MesseLeadsLlamaBridge"
FRAMEWORK_NAME="MesseLeadsLlamaBridge"
MIN_IOS_VERSION="15.0"

SOURCE_FILE="$SCRIPT_DIR/messeleads_llama_bridge.cpp"
HEADER_FILE="$SCRIPT_DIR/messeleads_llama_bridge.h"

DEVICE_LLAMA="$LLAMA_XCFRAMEWORK/ios-arm64/llama.framework"
SIMULATOR_LLAMA="$LLAMA_XCFRAMEWORK/ios-arm64_x86_64-simulator/llama.framework"

for path in "$SOURCE_FILE" "$HEADER_FILE" "$DEVICE_LLAMA/llama" "$SIMULATOR_LLAMA/llama"; do
    if [[ ! -e "$path" ]]; then
        echo "FEHLER: Erforderliche Datei fehlt: $path" >&2
        exit 1
    fi
done

rm -rf "$BUILD_DIR" "$OUTPUT_XCFRAMEWORK"
mkdir -p "$BUILD_DIR/device" "$BUILD_DIR/simulator"

create_framework_layout() {
    local destination="$1"
    local platform_name="$2"
    local supported_platform="$3"
    local supported_variant="$4"

    local framework="$destination/$FRAMEWORK_NAME.framework"
    mkdir -p "$framework/Headers" "$framework/Modules"

    cp "$HEADER_FILE" "$framework/Headers/$FRAMEWORK_NAME.h"

    cat > "$framework/Modules/module.modulemap" <<MODULEMAP
framework module $FRAMEWORK_NAME {
    umbrella header "$FRAMEWORK_NAME.h"
    export *
    module * { export * }
}
MODULEMAP

    cat > "$framework/Info.plist" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
    <key>CFBundleDevelopmentRegion</key>
    <string>en</string>
    <key>CFBundleExecutable</key>
    <string>$FRAMEWORK_NAME</string>
    <key>CFBundleIdentifier</key>
    <string>de.beckheun.messeleads.llamabridge</string>
    <key>CFBundleInfoDictionaryVersion</key>
    <string>6.0</string>
    <key>CFBundleName</key>
    <string>$FRAMEWORK_NAME</string>
    <key>CFBundlePackageType</key>
    <string>FMWK</string>
    <key>CFBundleShortVersionString</key>
    <string>1.0.0</string>
    <key>CFBundleVersion</key>
    <string>1</string>
    <key>MinimumOSVersion</key>
    <string>$MIN_IOS_VERSION</string>
    <key>DTPlatformName</key>
    <string>$platform_name</string>
    <key>CFBundleSupportedPlatforms</key>
    <array>
        <string>$supported_platform</string>
    </array>
PLIST

    if [[ -n "$supported_variant" ]]; then
        cat >> "$framework/Info.plist" <<PLIST
    <key>DTPlatformVariant</key>
    <string>$supported_variant</string>
PLIST
    fi

    cat >> "$framework/Info.plist" <<'PLIST'
</dict>
</plist>
PLIST
}

create_framework_layout "$BUILD_DIR/device" "iphoneos" "iPhoneOS" ""
create_framework_layout "$BUILD_DIR/simulator" "iphonesimulator" "iPhoneSimulator" "simulator"

DEVICE_FRAMEWORK="$BUILD_DIR/device/$FRAMEWORK_NAME.framework"
SIMULATOR_FRAMEWORK="$BUILD_DIR/simulator/$FRAMEWORK_NAME.framework"

COMMON_FLAGS=(
    -std=c++17
    -fvisibility=hidden
    -fvisibility-inlines-hidden
    -fno-objc-arc
    -dynamiclib
    -stdlib=libc++
    -I"$DEVICE_LLAMA/Headers"
    -install_name "@rpath/$FRAMEWORK_NAME.framework/$FRAMEWORK_NAME"
    -Wl,-rpath,@executable_path/Frameworks
    -Wl,-rpath,@loader_path/..
    -Wl,-rpath,@loader_path
)

xcrun --sdk iphoneos clang++ \
    "${COMMON_FLAGS[@]}" \
    -arch arm64 \
    -miphoneos-version-min="$MIN_IOS_VERSION" \
    -F"$(dirname "$DEVICE_LLAMA")" \
    -framework llama \
    "$SOURCE_FILE" \
    -o "$DEVICE_FRAMEWORK/$FRAMEWORK_NAME"

SIM_COMMON_FLAGS=(
    -std=c++17
    -fvisibility=hidden
    -fvisibility-inlines-hidden
    -fno-objc-arc
    -dynamiclib
    -stdlib=libc++
    -I"$SIMULATOR_LLAMA/Headers"
    -install_name "@rpath/$FRAMEWORK_NAME.framework/$FRAMEWORK_NAME"
    -Wl,-rpath,@executable_path/Frameworks
    -Wl,-rpath,@loader_path/..
    -Wl,-rpath,@loader_path
)

xcrun --sdk iphonesimulator clang++ \
    "${SIM_COMMON_FLAGS[@]}" \
    -arch arm64 \
    -arch x86_64 \
    -mios-simulator-version-min="$MIN_IOS_VERSION" \
    -F"$(dirname "$SIMULATOR_LLAMA")" \
    -framework llama \
    "$SOURCE_FILE" \
    -o "$SIMULATOR_FRAMEWORK/$FRAMEWORK_NAME"

codesign --force --sign - "$DEVICE_FRAMEWORK"
codesign --force --sign - "$SIMULATOR_FRAMEWORK"

xcodebuild -create-xcframework \
    -framework "$DEVICE_FRAMEWORK" \
    -framework "$SIMULATOR_FRAMEWORK" \
    -output "$OUTPUT_XCFRAMEWORK"

echo
echo "Bridge erfolgreich erstellt:"
echo "$OUTPUT_XCFRAMEWORK"
echo
echo "Exportierte Bridge-Symbole (Device):"
nm -gU "$DEVICE_FRAMEWORK/$FRAMEWORK_NAME" | grep "_ml_llama_" || true

echo
echo "Abhängigkeiten (Device):"
otool -L "$DEVICE_FRAMEWORK/$FRAMEWORK_NAME"
