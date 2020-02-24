#!/bin/bash

# Required env vars:
#export AWS_ACCESS_KEY_ID=$(get_octopusvariable "OctopusToolsAwsAccount.AccessKey")
#export AWS_SECRET_ACCESS_KEY=$(get_octopusvariable "OctopusToolsAwsAccount.SecretKey")
#export S3_PUBLISH_ENDPOINT=$(get_octopusvariable "Publish.APT.S3.TargetBucket")
#export GPG_PRIVATEKEY=$(get_octopusvariable "Publish.APT.GPG.PrivateKey")
#export GPG_PASSPHRASE=$(get_octopusvariable "Publish.APT.GPG.PassPhrase")

echo "Importing private key"
echo "$GPG_PRIVATEKEY" | gpg1 --batch --import || exit
curl -s "https://s3.amazonaws.com/$S3_PUBLISH_ENDPOINT/public.key" | gpg1 --no-default-keyring --keyring trustedkeys.gpg --import || exit

echo "Configuring S3 bucket"
#aws s3 mb "s3://$S3_PUBLISH_ENDPOINT" || exit 1
#aws s3api wait bucket-exists --bucket "$S3_PUBLISH_ENDPOINT" || exit 1
#aws s3 sync ./apt-content "s3://$S3_PUBLISH_ENDPOINT" --acl public-read || exit 1
aptly config show | jq '.S3PublishEndpoints[env.S3_PUBLISH_ENDPOINT] = {"region": "us-east-1", "bucket": env.S3_PUBLISH_ENDPOINT, "acl": "public-read"}' > ~/.aptly.conf.new || exit
mv ~/.aptly.conf.new ~/.aptly.conf || exit

echo "Creating APT repo"
aptly repo create -distribution=stretch -component=main octopus || exit

echo "Importing from existing APT repo"
aptly mirror create octopus-mirror "https://s3.amazonaws.com/$S3_PUBLISH_ENDPOINT/" stretch || exit
aptly mirror update octopus-mirror || exit
aptly repo import octopus-mirror octopus '$Version' || exit

echo "Adding new packages"
aptly repo add octopus ./OctopusTools.Packages.linux-x64 || exit
aptly repo show -with-packages octopus || exit

aptly publish repo -batch -passphrase "$GPG_PASSPHRASE" octopus "s3:$S3_PUBLISH_ENDPOINT:" || exit
