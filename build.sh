#!/bin/bash

# exit if error
set -o errexit

# define a few variables
node_output_name="Breeze-$os_platform-$arch"
app_output_name="breeze-$TRAVIS_OS_NAME-$arch"
app_output_zip_name="breeze-$TRAVIS_OS_NAME-$arch.zip"

if [ "$TRAVIS_OS_NAME" = "osx" ]
then
  dotnet_resources_path_in_app=$TRAVIS_BUILD_DIR/breeze_out/$app_output_name/Breeze.app/contents/resources/app/assets/daemon
else
  dotnet_resources_path_in_app=$TRAVIS_BUILD_DIR/breeze_out/$app_output_name/resources/app/assets/daemon
fi

echo "current environment variables:"
echo "OS name:" $TRAVIS_OS_NAME
echo "Platform:" $os_platform
echo "Build directory:" $TRAVIS_BUILD_DIR
echo "Node version:" $TRAVIS_NODE_VERSION
echo "Architecture:" $arch
echo "Configuration:" $configuration
echo "Node.js output name:" $node_output_name
echo "App output folder name:" $app_output_name
echo "App output zip file name:" $app_output_zip_name
echo "dotnet resources path in app:" $dotnet_resources_path_in_app
echo "Branch:" $TRAVIS_BRANCH
echo "Tag:" $TRAVIS_TAG
echo "Commit:" $TRAVIS_COMMIT
echo "Commit message:" $TRAVIS_COMMIT_MESSAGE


dotnet --info

# Initialize dependencies
echo $log_prefix STARTED restoring dotnet and npm packages
cd $TRAVIS_BUILD_DIR
git submodule update --init --recursive

cd $TRAVIS_BUILD_DIR/Breeze.UI

npm install
echo $log_prefix FINISHED restoring dotnet and npm packages

# dotnet publish
echo $log_prefix running 'dotnet publish'
cd $TRAVIS_BUILD_DIR/StratisBitcoinFullNode/Stratis.BreezeD
dotnet publish -c $configuration -r $TRAVIS_OS_NAME-$arch -v m -o $TRAVIS_BUILD_DIR/dotnet_out/$TRAVIS_OS_NAME

echo $log_prefix chmoding the Stratis.BreezeD file
chmod +x $TRAVIS_BUILD_DIR/dotnet_out/$TRAVIS_OS_NAME/Stratis.BreezeD

# node Build
cd $TRAVIS_BUILD_DIR/Breeze.UI
echo $log_prefix running 'npm run'
npm run build:prod

# node packaging
echo $log_prefix packaging breeze 
node package.js --platform=$os_platform --arch=$arch --path=$TRAVIS_BUILD_DIR/breeze_out

# rename node generated folder
echo $log_prefix rename the folder generated by npm from $node_output_name to $app_output_name 
mv $TRAVIS_BUILD_DIR/breeze_out/$node_output_name $TRAVIS_BUILD_DIR/breeze_out/$app_output_name 

# copy api libs into app
echo $log_prefix copying the Breeze api into the app
mkdir -p $dotnet_resources_path_in_app
cp -r $TRAVIS_BUILD_DIR/dotnet_out/$TRAVIS_OS_NAME/* $dotnet_resources_path_in_app

# zip result
echo $log_prefix zipping the app into $TRAVIS_BUILD_DIR/breeze_out/$app_output_zip_name
mkdir -p $TRAVIS_BUILD_DIR/deploy/
cd $TRAVIS_BUILD_DIR/breeze_out
zip -r $TRAVIS_BUILD_DIR/deploy/$app_output_zip_name $app_output_name/*

#tests
echo $log_prefix no tests to run

cd $TRAVIS_BUILD_DIR
ls

echo $log_prefix FINISHED build

