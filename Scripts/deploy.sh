sh ./Scripts/install_unity.sh
cd ./Promises/Promises/
ls /Applications/Unity
ls /Applications/Unity/Unity.app
python -c "import os; print(os.path.is_file('/Applications/Unity/Unity.app/Contents/MacOS/Unity'))"
uget build -p ./Promises.csproj --debug
uget create -p ./Promises.csproj --debug
uget pack -p ./Promises.csproj --debug