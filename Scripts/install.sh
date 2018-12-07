#! /bin/sh

# Download Unity3D installer into the container
#  The below link will need to change depending on the version, this one is for 5.5.1
#  Refer to https://unity3d.com/get-unity/download/archive and find the link pointed to by Mac "Unity Editor"
echo 'Downloading Unity:'
curl --retry 5 -o Unity.pkg https://download.unity3d.com/download_unity/65e0713a5949/MacEditorInstaller/Unity-2018.2.15f1.pkg
if [ $? -ne 0 ]; then { echo "Download failed"; exit $?; } fi

# Run installer(s)
echo 'Installing Unity.pkg'
sudo installer -dumplog -package Unity.pkg -target /