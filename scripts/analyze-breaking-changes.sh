#!/bin/bash
set -e

FROM_REF=${1:-HEAD~1}
TO_REF=${2:-HEAD}

echo "Analyzing changes from $FROM_REF to $TO_REF"

# Get list of changed C# files
CHANGED_FILES=$(git diff --name-only "$FROM_REF" "$TO_REF" -- "*.cs" 2>/dev/null || echo "")

if [ -z "$CHANGED_FILES" ]; then
  echo "No C# files changed"
  echo "none"
  exit 0
fi

echo "Changed files:"
echo "$CHANGED_FILES"

# Analyze the diff for breaking changes
DIFF=$(git diff "$FROM_REF" "$TO_REF" -- "*.cs" 2>/dev/null || echo "")

# Check for major breaking changes
if echo "$DIFF" | grep -qE "^-.*public (class|interface|enum|struct)"; then
  echo "Detected removed public types - MAJOR change"
  echo "major"
  exit 0
fi

if echo "$DIFF" | grep -qE "^-.*public.*\("; then
  echo "Detected removed public methods - MAJOR change"
  echo "major"
  exit 0
fi

# Check for minor changes (new features)
if echo "$DIFF" | grep -qE "^\+.*public (class|interface|enum|struct)"; then
  echo "Detected new public types - MINOR change"
  echo "minor"
  exit 0
fi

if echo "$DIFF" | grep -qE "^\+.*public.*\("; then
  echo "Detected new public methods - MINOR change"
  echo "minor"
  exit 0
fi

# Check for namespace changes (breaking)
if echo "$DIFF" | grep -qE "^-namespace|^\+namespace"; then
  OLD_NS=$(echo "$DIFF" | grep "^-namespace" | head -1)
  NEW_NS=$(echo "$DIFF" | grep "^\+namespace" | head -1)
  if [ "$OLD_NS" != "$NEW_NS" ] && [ -n "$OLD_NS" ]; then
    echo "Detected namespace change - MAJOR change"
    echo "major"
    exit 0
  fi
fi

# Default to patch
echo "No breaking changes detected - PATCH change"
echo "patch"

