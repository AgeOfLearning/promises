sh ./Scripts/install_unity.sh
uget build -p ./Promises/Promises/Promises.csproj --config-path ./Promises/Promises/uget.config.json
uget create -p ./Promises/Promises/Promises.csproj --config-path ./Promises/Promises/uget.config.json
uget pack -p ./Promises/Promises/Promises.csproj --config-path ./Promises/Promises/uget.config.json