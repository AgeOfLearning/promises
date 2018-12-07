sh ./Scripts/install_unity.sh
cd ./Promises/Promises/
uget build -p ./Promises.csproj --configuration=Release
uget create -p ./Promises.csproj --configuration=Release
uget pack -p ./Promises.csproj --configuration=Release