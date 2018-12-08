#! /bin/sh

# Download Unity3D installer into the container
#  The below link will need to change depending on the version, this one is for 5.5.1
#  Refer to https://unity3d.com/get-unity/download/archive and find the link pointed to by Mac "Unity Editor"
UNITY_PACKAGE_URL=https://download.unity3d.com/download_unity/65e0713a5949/MacEditorInstaller/Unity-2018.2.15f1.pkg

#download package if it does not already exist in cache
#if [ ! -e $UNITY_DOWNLOAD_CACHE/Unity.pkg ] ; then
    echo 'Downloading Unity:'
    curl --retry 5 -o $UNITY_DOWNLOAD_CACHE/Unity.pkg $UNITY_PACKAGE_URL
    if [ $? -ne 0 ]; then { echo "Download failed"; exit $?; } fi
#fi

# Run installer(s)
echo 'Installing Unity.pkg'
sudo installer -dumplog -package $UNITY_DOWNLOAD_CACHE/Unity.pkg -target /