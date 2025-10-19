.PHONY: build-macos clean-macos publish-macos dmg help sync-plist all-macos

# Configuration
MACOS_PROJECT := macOS/NX_Game_Info.csproj
BUILD_CONFIG := Release
BUILD_DIR := macOS/bin/$(BUILD_CONFIG)/net9.0-macos15.0
METADATA := $(shell dotnet msbuild $(MACOS_PROJECT) -nologo -t:PrintBuildMetadata | sed 's/^[[:space:]]*//' | tr '\n' ' ')
VERSION := $(word 2,$(subst =, ,$(firstword $(filter GameInfoVersion=%,$(METADATA)))))
BUILD_NUMBER := $(word 2,$(subst =, ,$(firstword $(filter GameInfoBuildNumber=%,$(METADATA)))))
DMG_NAME := NX_Game_Info-$(VERSION).dmg

help:
	@echo "NX Game Info - macOS Build Targets"
	@echo ""
	@echo "  make build-macos    - Build the macOS application"
	@echo "  make publish-macos  - Create distributable packages"
	@echo "  make dmg            - Create DMG disk image"
	@echo "  make clean-macos    - Clean build artifacts"
	@echo "  make all-macos      - Build, publish, and create DMG"
	@echo ""

clean-macos:
	@echo "🧹 Cleaning macOS build..."
	@dotnet clean $(MACOS_PROJECT) --configuration $(BUILD_CONFIG)

build-macos:
		@echo "🔨 Building macOS app..."
		@$(MAKE) -s sync-plist
		@dotnet restore $(MACOS_PROJECT) --nologo
		@dotnet build $(MACOS_PROJECT) --configuration $(BUILD_CONFIG) --no-restore --nologo

publish-macos: build-macos
		@echo "📤 Publishing macOS packages..."
		@dotnet publish $(MACOS_PROJECT) --configuration $(BUILD_CONFIG) --no-restore -p:SelfContained=true -p:CreatePackage=true --nologo

dmg: publish-macos
		@echo "💿 Creating DMG..."
		@hdiutil create -volname "NX Game Info" \
			-srcfolder "$(BUILD_DIR)/osx-arm64/NX Game Info.app" \
			-ov -format UDZO \
			"macOS/bin/$(BUILD_CONFIG)/$(DMG_NAME)"
		@echo "✅ DMG created: macOS/bin/$(BUILD_CONFIG)/$(DMG_NAME)"

all-macos: clean-macos dmg
	@echo "✅ macOS build complete!"
		@echo ""
		@echo "📦 Distribution files:"
		@echo "  • DMG: macOS/bin/$(BUILD_CONFIG)/$(DMG_NAME)"
		@echo "  • PKG: $(BUILD_DIR)/osx-arm64/publish/NX Game Info-$(VERSION).pkg"

sync-plist:
		@echo "📝 Syncing Info.plist metadata..."
		@/usr/libexec/PlistBuddy -c "Set :CFBundleShortVersionString $(VERSION)" macOS/Info.plist 2>/dev/null || /usr/libexec/PlistBuddy -c "Add :CFBundleShortVersionString string $(VERSION)" macOS/Info.plist
		@/usr/libexec/PlistBuddy -c "Set :CFBundleVersion $(BUILD_NUMBER)" macOS/Info.plist 2>/dev/null || /usr/libexec/PlistBuddy -c "Add :CFBundleVersion string $(BUILD_NUMBER)" macOS/Info.plist
