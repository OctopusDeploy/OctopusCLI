#!/bin/bash

# Required env vars:
#export AWS_ACCESS_KEY_ID=$(get_octopusvariable "OctopusToolsAwsAccount.AccessKey")
#export AWS_SECRET_ACCESS_KEY=$(get_octopusvariable "OctopusToolsAwsAccount.SecretKey")
#export S3_PUBLISH_ENDPOINT=$(get_octopusvariable "Publish.APT.S3.TargetBucket")

if [ ! -z "${DEBUG}" ]; then
  set -x
fi

DEPENDENCIES=("aws" "createrepo")
for dep in "${DEPENDENCIES[@]}"
do
  if [ ! $(which ${dep}) ]; then
      echo "${dep} must be available."
      exit 1
  fi
done

echo "Configuring S3 bucket"
#aws s3 mb "s3://${S3_PUBLISH_ENDPOINT}"|| echo ERROR
#aws s3api wait bucket-exists --bucket ${S3_PUBLISH_ENDPOINT}|| echo ERROR
#aws s3 sync ./rpm-content "s3://${S3_PUBLISH_ENDPOINT}" --acl public-read|| echo ERROR

TARGET_DIR="/tmp/$S3_PUBLISH_ENDPOINT"

# make sure we're operating on the latest data in the target bucket
rm -rf "$TARGET_DIR"|| exit 1
mkdir -p "$TARGET_DIR"|| exit 1
aws s3 sync "s3://$S3_PUBLISH_ENDPOINT" "$TARGET_DIR"|| exit 1

# copy the RPM in and update the repo
mkdir -pv "$TARGET_DIR/x86_64/"|| exit 1
cp -v OctopusTools.Packages.linux-x64/*.rpm "$TARGET_DIR/x86_64/"|| exit 1
UPDATE=""
if [ -e "$TARGET_DIR/x86_64/repodata/repomd.xml" ]; then
  UPDATE="--update "
fi
for a in "$TARGET_DIR/x86_64"; do
  createrepo -v $UPDATE --deltas "$a/"|| exit 1
done

# sync the repo state back to s3
aws s3 sync "$TARGET_DIR" "s3://$S3_PUBLISH_ENDPOINT" --acl public-read --delete|| exit 1
