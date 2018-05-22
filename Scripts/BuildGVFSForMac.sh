#!/bin/bash

CONFIGURATION=$1
if [ -z $CONFIGURATION ]; then
  CONFIGURATION=Debug
fi

SCRIPTDIR=$(dirname ${BASH_SOURCE[0]})

xcodebuild -sdk macosx10.13 -configuration $CONFIGURATION -workspace $SCRIPTDIR/../Mac/PrjFS.xcworkspace build -scheme PrjFS -derivedDataPath $SCRIPTDIR/../../BuildOutput/Mac
dotnet build $SCRIPTDIR/../Mac/MirrorProvider/MirrorProvider.sln --configuration $CONFIGURATION
