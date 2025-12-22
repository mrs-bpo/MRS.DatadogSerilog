#!/bin/bash
set -e

EVENT_NAME=$1
REF_NAME=$2
IS_PR=${3:-false}

echo "Event: $EVENT_NAME, Ref: $REF_NAME, Is PR: $IS_PR"

# Get the latest tag, default to 0.0.0 if none exists
LATEST_TAG=$(git describe --tags --abbrev=0 2>/dev/null || echo "v0.0.0")
echo "Latest tag: $LATEST_TAG"

# Remove 'v' prefix if present
LATEST_VERSION=${LATEST_TAG#v}

# Split version into parts
IFS='.' read -r MAJOR MINOR PATCH <<< "$LATEST_VERSION"

# Remove any prerelease suffix from patch
PATCH=${PATCH%%-*}

echo "Current version: $MAJOR.$MINOR.$PATCH"

# Determine version type by analyzing commits since last tag
VERSION_TYPE="patch"
SHOULD_PUBLISH="false"
IS_PRERELEASE="false"

# Check if there are any commits since the last tag
if [ -n "$(git rev-list ${LATEST_TAG}..HEAD 2>/dev/null)" ]; then
  echo "Found commits since last tag"
  
  # Run breaking changes analysis
  if [ -f "./scripts/analyze-breaking-changes.sh" ]; then
    chmod +x ./scripts/analyze-breaking-changes.sh
    BREAKING_CHANGES=$(./scripts/analyze-breaking-changes.sh "$LATEST_TAG" "HEAD" || echo "none")
    echo "Breaking changes analysis: $BREAKING_CHANGES"
    
    if [ "$BREAKING_CHANGES" == "major" ]; then
      VERSION_TYPE="major"
    elif [ "$BREAKING_CHANGES" == "minor" ]; then
      VERSION_TYPE="minor"
    fi
  fi
  
  # Check commit messages for version hints
  COMMITS=$(git log ${LATEST_TAG}..HEAD --pretty=format:"%s" 2>/dev/null || echo "")
  
  if echo "$COMMITS" | grep -qiE "^(BREAKING CHANGE:|major:)"; then
    VERSION_TYPE="major"
  elif echo "$COMMITS" | grep -qiE "^(feat:|feature:|minor:)"; then
    VERSION_TYPE="minor"
  fi
fi

# Calculate new version
if [ "$VERSION_TYPE" == "major" ]; then
  MAJOR=$((MAJOR + 1))
  MINOR=0
  PATCH=0
elif [ "$VERSION_TYPE" == "minor" ]; then
  MINOR=$((MINOR + 1))
  PATCH=0
else
  PATCH=$((PATCH + 1))
fi

NEW_VERSION="$MAJOR.$MINOR.$PATCH"

# Handle feature branches and PRs
if [[ "$REF_NAME" == feature/* ]]; then
  FEATURE_NAME=$(echo "$REF_NAME" | sed 's/feature\///' | sed 's/[^a-zA-Z0-9]/-/g')
  NEW_VERSION="$NEW_VERSION-$FEATURE_NAME.$GITHUB_RUN_NUMBER"
  IS_PRERELEASE="true"
  SHOULD_PUBLISH="false"
elif [ "$IS_PR" == "true" ]; then
  NEW_VERSION="$NEW_VERSION-pr.$GITHUB_RUN_NUMBER"
  IS_PRERELEASE="true"
  SHOULD_PUBLISH="false"
elif [[ "$REF_NAME" == "main" ]] || [[ "$REF_NAME" == "master" ]]; then
  # Auto-publish on main/master branch
  SHOULD_PUBLISH="true"
elif [[ "$EVENT_NAME" == "workflow_dispatch" ]]; then
  # Manual trigger
  SHOULD_PUBLISH="true"
fi

echo "New version: $NEW_VERSION"
echo "Version type: $VERSION_TYPE"
echo "Should publish: $SHOULD_PUBLISH"
echo "Is prerelease: $IS_PRERELEASE"

# Output to GitHub Actions
echo "version=$NEW_VERSION" >> $GITHUB_OUTPUT
echo "version-type=$VERSION_TYPE" >> $GITHUB_OUTPUT
echo "is-prerelease=$IS_PRERELEASE" >> $GITHUB_OUTPUT
echo "should-publish=$SHOULD_PUBLISH" >> $GITHUB_OUTPUT

