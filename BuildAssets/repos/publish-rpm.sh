#!/bin/bash

if [[ -z "$PUBLISH_LINUX_EXTERNAL" || -z "$PUBLISH_ARTIFACTORY_USERNAME" || -z "$PUBLISH_ARTIFACTORY_PASSWORD" \
   || -z "$AWS_ACCESS_KEY_ID" || -z "$AWS_SECRET_ACCESS_KEY" ]]; then
  echo -e 'This script requires the following environment variables to be set:
  PUBLISH_LINUX_EXTERNAL, PUBLISH_ARTIFACTORY_USERNAME, PUBLISH_ARTIFACTORY_PASSWORD, AWS_ACCESS_KEY_ID, AWS_SECRET_ACCESS_KEY' >&2
  exit 1
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  REPO_KEY='rpm'
  BUCKET='rpm.octopus.com'
  ORIGIN="https://$BUCKET"
else
  REPO_KEY='rpm-prerelease'
  BUCKET='prerelease.rpm.octopus.com'
  ORIGIN="http://$BUCKET"
fi
CURL_UPL_OPTS=(--silent --show-error --fail --user "$PUBLISH_ARTIFACTORY_USERNAME:$PUBLISH_ARTIFACTORY_PASSWORD")

echo "Uploading config to Artifactory"
REPO_BODY="baseurl=$ORIGIN/\$basearch/
enabled=1
gpgkey=$ORIGIN/public.key
gpgcheck=0
repo_gpgcheck=1
"
echo "[tentacle]
name=Octopus Tentacle
$REPO_BODY" | curl "${CURL_UPL_OPTS[@]}" --request PUT --upload-file - \
  "https://octopusdeploy.jfrog.io/octopusdeploy/rpm-prerelease/tentacle.repo" || exit
echo "[octopuscli]
name=Octopus CLI
$REPO_BODY" | curl "${CURL_UPL_OPTS[@]}" --request PUT --upload-file - \
  "https://octopusdeploy.jfrog.io/octopusdeploy/rpm-prerelease/octopuscli.repo" || exit

echo "Uploading package to Artifactory"
PKG=$(ls -1 *.rpm | head -n1)
PKGBN=$(basename "$PKG")
curl "${CURL_UPL_OPTS[@]}" --request PUT --upload-file "$PKG" \
  "https://octopusdeploy.jfrog.io/octopusdeploy/$REPO_KEY/x86_64/$PKGBN" \
  || exit

echo "Waiting for reindex"
sleep 5
# Note: reindex is automatic, but triggering it synchronously provides an indication when it has completed
curl "${CURL_UPL_OPTS[@]}" --request POST \
  "https://octopusdeploy.jfrog.io/octopusdeploy/api/yum/$REPO_KEY?async=0" || exit

echo "Preparing sync to S3"
RCLONE_OPTS=(--config=/dev/null --verbose --s3-provider=AWS --s3-env-auth=true --s3-region=us-east-1 --s3-acl=public-read)
RCLONE_SYNC_OPTS=(:http: ":s3:$BUCKET" --http-url="https://octopusdeploy.jfrog.io/octopusdeploy/$REPO_KEY" "${RCLONE_OPTS[@]}" \
  --fast-list --update --use-server-modtime)
rclone sync "${RCLONE_SYNC_OPTS[@]}" --dry-run --include=*.rpm --max-delete=0 2>&1 \
  || { echo 'Package deletion predicted. Aborting sync to S3 for manual investigation.' >&2; exit 1; }

echo "Copying new files to S3"
rclone copy "${RCLONE_SYNC_OPTS[@]}" --ignore-existing 2>&1 || exit

echo "Replacing changed files then deleting on S3"
rclone sync "${RCLONE_SYNC_OPTS[@]}" --delete-after 2>&1 || exit

echo "Asserting current public key on S3"
curl --silent --show-error --fail https://octopusdeploy.jfrog.io/octopusdeploy/api/gpg/key/public \
  | rclone rcat ":s3:$BUCKET/public.key" "${RCLONE_OPTS[@]}" 2>&1 || exit
