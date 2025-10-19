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
		@APP_DIR="$(BUILD_DIR)/osx-arm64"; \
		 APP_PATH="$$APP_DIR/NX Game Info.app"; \
		 DMG_OUT="macOS/bin/$(BUILD_CONFIG)/$(DMG_NAME)"; \
		 if command -v create-dmg >/dev/null 2>&1; then \
		   echo "➡ Using create-dmg"; \
		   rm -f "$$DMG_OUT"; \
		   create-dmg \
		     --volname "NX Game Info" \
		     --volicon "$$APP_PATH/Contents/Resources/AppIcon.icns" \
		     --window-pos 200 120 --window-size 600 400 \
		     --icon-size 96 \
		     --icon "NX Game Info.app" 110 150 \
		     --app-drop-link 480 150 \
		     --overwrite \
		     "$$DMG_OUT" "$$APP_DIR"; \
		 else \
		   echo "➡ Using hdiutil fallback"; \
		   STAGING_DIR="$$(mktemp -d)"; \
		   cp -R "$$APP_PATH" "$$STAGING_DIR/"; \
		   ln -s /Applications "$$STAGING_DIR/Applications"; \
		   hdiutil create -volname "NX Game Info" \
		     -srcfolder "$$STAGING_DIR" \
		     -ov -format UDZO \
		     "$$DMG_OUT"; \
		   rm -rf "$$STAGING_DIR"; \
		 fi; \
		 echo "✅ DMG created: $$DMG_OUT"

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
