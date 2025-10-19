#!/bin/bash
set -e

echo "🔨 Building NX Game Info for macOS..."

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
MACOS_PROJECT="$PROJECT_DIR/macOS/NX_Game_Info.csproj"
BUILD_CONFIG="Release"

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
RED='\033[0;31m'
NC='\033[0m' # No Color

build_metadata=$(dotnet msbuild "$MACOS_PROJECT" -nologo -t:PrintBuildMetadata)
VERSION=$(printf '%s\n' "$build_metadata" | awk -F= '/GameInfoVersion=/ {print $2; exit}')
BUILD_NUMBER=$(printf '%s\n' "$build_metadata" | awk -F= '/GameInfoBuildNumber=/ {print $2; exit}')

if [ -z "$VERSION" ] || [ -z "$BUILD_NUMBER" ]; then
    echo -e "${RED}❌ Unable to determine build metadata from MSBuild.${NC}"
    exit 1
fi

INFO_PLIST="$PROJECT_DIR/macOS/Info.plist"
/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $VERSION" "$INFO_PLIST" 2>/dev/null || \
    /usr/libexec/PlistBuddy -c "Add :CFBundleShortVersionString string $VERSION" "$INFO_PLIST"
/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $BUILD_NUMBER" "$INFO_PLIST" 2>/dev/null || \
    /usr/libexec/PlistBuddy -c "Add :CFBundleVersion string $BUILD_NUMBER" "$INFO_PLIST"

echo -e "${BLUE}📦 Cleaning previous builds...${NC}"
dotnet clean "$MACOS_PROJECT" --configuration "$BUILD_CONFIG"

echo -e "${BLUE}🔧 Building project...${NC}"
dotnet build "$MACOS_PROJECT" --configuration "$BUILD_CONFIG"

echo -e "${BLUE}📤 Publishing for distribution...${NC}"
dotnet publish "$MACOS_PROJECT" --configuration "$BUILD_CONFIG" -p:SelfContained=true -p:CreatePackage=true

echo -e "${BLUE}💿 Creating DMG...${NC}"
DMG_NAME="NX_Game_Info-${VERSION}.dmg"
DMG_PATH="$PROJECT_DIR/macOS/bin/$BUILD_CONFIG/$DMG_NAME"
hdiutil create -volname "NX Game Info" \
    -srcfolder "$PROJECT_DIR/macOS/bin/$BUILD_CONFIG/net9.0-macos15.0/osx-arm64/NX Game Info.app" \
    -ov -format UDZO \
    "$DMG_PATH"

echo ""
echo -e "${GREEN}✅ Build complete!${NC}"
echo ""
echo "📦 Distribution files:"
echo "  • DMG:  $DMG_PATH"
echo "  • PKG:  $PROJECT_DIR/macOS/bin/$BUILD_CONFIG/net9.0-macos15.0/osx-arm64/publish/NX Game Info-$VERSION.pkg"
echo "  • .app: $PROJECT_DIR/macOS/bin/$BUILD_CONFIG/net9.0-macos15.0/osx-arm64/NX Game Info.app"
echo ""
echo "🚀 To install:"
echo "  1. Open the DMG file"
echo "  2. Drag 'NX Game Info.app' to Applications folder"
echo "  3. Right-click and select 'Open' on first launch (if needed)"
