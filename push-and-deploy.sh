#!/bin/sh

set -e

git status
git add -A
git commit -m "$(date -Is)"
git push -u origin gh-pages

GIT_USER=pimaker USE_SSH=true npm run deploy
