sh ./Scripts/install_unity.sh
cd ./Promises/Promises/
uget build -p ./Promises.csproj -c Release
uget create -p ./Promises.csproj -c Release
uget pack -p ./Promises.csproj -c Release