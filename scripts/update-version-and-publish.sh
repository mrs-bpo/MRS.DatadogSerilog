#!/bin/bash
set -e

NEW_VERSION=$1
VERSION_TYPE=$2
IS_PRERELEASE=$3
PACKAGE_NAME=$4

echo "Updating to version: $NEW_VERSION"
echo "Version type: $VERSION_TYPE"
echo "Is prerelease: $IS_PRERELEASE"
echo "Package name: $PACKAGE_NAME"

# Find the .csproj file
CSPROJ_FILE=$(find . -name "*.csproj" -not -path "*/bin/*" -not -path "*/obj/*" | head -1)

if [ -z "$CSPROJ_FILE" ]; then
  echo "Error: Could not find .csproj file"
  exit 1
fi

echo "Found project file: $CSPROJ_FILE"

# Update version in .csproj file
if grep -q "<Version>" "$CSPROJ_FILE"; then
  # Version tag exists, update it
  sed -i "s|<Version>.*</Version>|<Version>$NEW_VERSION</Version>|g" "$CSPROJ_FILE"
  echo "Updated existing Version tag"
else
  # Add Version tag to PropertyGroup
  sed -i "s|</PropertyGroup>|  <Version>$NEW_VERSION</Version>\n  </PropertyGroup>|" "$CSPROJ_FILE"
  echo "Added Version tag to project file"
fi

# Update PackageVersion if it exists
if grep -q "<PackageVersion>" "$CSPROJ_FILE"; then
  sed -i "s|<PackageVersion>.*</PackageVersion>|<PackageVersion>$NEW_VERSION</PackageVersion>|g" "$CSPROJ_FILE"
  echo "Updated PackageVersion tag"
fi

# Update AssemblyVersion if it exists
if grep -q "<AssemblyVersion>" "$CSPROJ_FILE"; then
  # For AssemblyVersion, use only Major.Minor.Build (without prerelease suffix)
  ASSEMBLY_VERSION=$(echo "$NEW_VERSION" | sed 's/-.*//').0
  sed -i "s|<AssemblyVersion>.*</AssemblyVersion>|<AssemblyVersion>$ASSEMBLY_VERSION</AssemblyVersion>|g" "$CSPROJ_FILE"
  echo "Updated AssemblyVersion to $ASSEMBLY_VERSION"
fi

echo "Version updated in project file"

# Only create git tag for non-prerelease versions
if [ "$IS_PRERELEASE" != "true" ]; then
  # Configure git
  git config user.name "github-actions[bot]"
  git config user.email "github-actions[bot]@users.noreply.github.com"
  
  # Handle tag collision by incrementing until we find an unused version
  TAG_NAME="v$NEW_VERSION"
  FINAL_VERSION="$NEW_VERSION"
  
  while git rev-parse "$TAG_NAME" >/dev/null 2>&1; do
    echo "Tag $TAG_NAME already exists, incrementing patch version..."
    
    # Parse version and increment patch
    IFS='.' read -r MAJOR MINOR PATCH <<< "$FINAL_VERSION"
    PATCH=${PATCH%%-*}  # Remove any prerelease suffix
    PATCH=$((PATCH + 1))
    FINAL_VERSION="$MAJOR.$MINOR.$PATCH"
    TAG_NAME="v$FINAL_VERSION"
    
    echo "Trying version: $FINAL_VERSION"
  done
  
  # If version changed due to collision, update the project file again
  if [ "$FINAL_VERSION" != "$NEW_VERSION" ]; then
    echo "Version collision detected, using $FINAL_VERSION instead of $NEW_VERSION"
    
    # Update version in csproj with final version
    if grep -q "<Version>" "$CSPROJ_FILE"; then
      sed -i "s|<Version>.*</Version>|<Version>$FINAL_VERSION</Version>|g" "$CSPROJ_FILE"
    fi
    if grep -q "<PackageVersion>" "$CSPROJ_FILE"; then
      sed -i "s|<PackageVersion>.*</PackageVersion>|<PackageVersion>$FINAL_VERSION</PackageVersion>|g" "$CSPROJ_FILE"
    fi
    
    NEW_VERSION="$FINAL_VERSION"
  fi
  
  # Commit the version change
  git add "$CSPROJ_FILE"
  git commit -m "chore: bump version to $NEW_VERSION [skip ci]" || echo "No changes to commit"
  
  # Create and push tag
  git tag -a "$TAG_NAME" -m "Release version $NEW_VERSION"
  echo "Created tag: $TAG_NAME"
  
  # Push both commit and tag
  git push origin HEAD:master || echo "Failed to push commit"
  git push origin "$TAG_NAME" || echo "Failed to push tag"
  
  # Output the final version for the pack step
  echo "FINAL_VERSION=$NEW_VERSION" >> $GITHUB_ENV
else
  echo "Skipping git tag creation for prerelease version"
fi

echo "Version update complete: $NEW_VERSION"

