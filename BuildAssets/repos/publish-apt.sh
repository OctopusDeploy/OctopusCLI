#!/bin/bash
# Publish the first .deb in the working directory to an Artifactory apt repository, then sync the repository to S3.

which curl rclone >/dev/null || {
  echo 'This script requires curl and rclone, found in the container "octopusdeploy/publish-linux".' >&2
  exit 1
}
if [[ -z "$PUBLISH_LINUX_EXTERNAL" ]]; then
  echo 'This script requires the environment variable PUBLISH_LINUX_EXTERNAL - specify "true" to publish to the external public feed.' >&2
  exit 1
fi
if [[ -z "$PUBLISH_ARTIFACTORY_USERNAME" || -z "$PUBLISH_ARTIFACTORY_PASSWORD" ]]; then
  echo -e 'This script requires the environment variables PUBLISH_ARTIFACTORY_USERNAME and PUBLISH_ARTIFACTORY_PASSWORD - an account on'\
    '\nthe artifactory instance with permissions up to "Manage" on the apt repos.' >&2
  exit 1
fi
if [[ -z "$AWS_ACCESS_KEY_ID" || -z "$AWS_SECRET_ACCESS_KEY" ]]; then
  echo -e 'This script requires the environment variables AWS_ACCESS_KEY_ID and AWS_SECRET_ACCESS_KEY - credentials to sync repo updates'\
    '\nto S3.' >&2
  exit 1
fi

if [[ "$PUBLISH_LINUX_EXTERNAL" == "true" ]]; then
  REPO_KEY='apt'
  BUCKET='apt.octopus.com'
  ORIGIN="https://$BUCKET"
else
  REPO_KEY='apt-prerelease'
  BUCKET='prerelease.apt.octopus.com'
  ORIGIN="http://$BUCKET"
fi
CURL_UPL_OPTS=(--silent --show-error --fail --user "$PUBLISH_ARTIFACTORY_USERNAME:$PUBLISH_ARTIFACTORY_PASSWORD")

echo "Uploading package to Artifactory"
PKG=$(set -o pipefail; ls -1 *.deb | head -n1) || exit
PKGBN=$(basename "$PKG")
DISTS=(oldoldstable oldstable stable jessie stretch buster trusty xenial bionic cosmic disco eoan)
DISTS=$(printf ";deb.distribution=%s" "${DISTS[@]}")
curl "${CURL_UPL_OPTS[@]}" --request PUT --upload-file "$PKG" \
  "https://octopusdeploy.jfrog.io/octopusdeploy/$REPO_KEY/pool/main/${PKGBN:0:1}/${PKGBN/%_*/}/$PKGBN$DISTS;deb.component=main;deb.architecture=amd64" \
  || exit

echo "Waiting for reindex"
sleep 5
# Note: reindex is automatic, but triggering it synchronously provides an indication when it has completed
curl "${CURL_UPL_OPTS[@]}" --request POST \
  "https://octopusdeploy.jfrog.io/octopusdeploy/api/deb/reindex/$REPO_KEY?async=0" || exit

echo "Preparing sync to S3"
RCLONE_OPTS=(--config=/dev/null --verbose --s3-provider=AWS --s3-env-auth=true --s3-region=us-east-1 --s3-acl=public-read)
RCLONE_SYNC_OPTS=(:http: ":s3:$BUCKET" --http-url="https://octopusdeploy.jfrog.io/octopusdeploy/$REPO_KEY" "${RCLONE_OPTS[@]}" \
  --fast-list --update --use-server-modtime)
rclone sync "${RCLONE_SYNC_OPTS[@]}" --dry-run --include=*.deb --max-delete=0 2>&1 \
  || { echo 'Package deletion predicted. Aborting sync to S3 for manual investigation.' >&2; exit 1; }

echo "Copying new files to S3"
rclone copy "${RCLONE_SYNC_OPTS[@]}" --ignore-existing 2>&1 || exit

echo "Replacing changed files then deleting on S3"
rclone sync "${RCLONE_SYNC_OPTS[@]}" --delete-after 2>&1 || exit

echo "Asserting current public key on S3"
set -o pipefail
curl --silent --show-error --fail --location https://octopusdeploy.jfrog.io/octopusdeploy/api/gpg/key/public \
  | rclone rcat ":s3:$BUCKET/public.key" "${RCLONE_OPTS[@]}" 2>&1 || exit
set +o pipefail
