sh ./Scripts/install_unity.sh
cd ./Promises/Promises/
ls /Applications/Unity
ls /Applications/Unity/Unity.app
uget build -p ./Promises.csproj --debug
uget create -p ./Promises.csproj --debug
uget pack -p ./Promises.csproj --debug