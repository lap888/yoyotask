#!/bin/bash
echo ">>进入打包项目路径"
cd /Users/topbrids/Desktop/lfex/yoyo/Yoyo/Yoyo.UserApi
echo ">>查看当前路径下文件"
ls
echo ">>执行打包发布命令"
dotnet publish -c release -o /Users/topbrids/Desktop/lfex/yoyo/Yoyo/release/yoyoba
echo ">>进入到打包好的路径查看"
cd /Users/topbrids/Desktop/lfex/yoyo/Yoyo/release/
ls
echo ">>打成压缩包"
tar -zcvf yoyoba.tar.gz yoyoba
echo ">>上传到服务器 请输入密码:"
scp -P 6000 -i /Users/topbrids/cert/yyb.pem yoyoba.tar.gz root@49.233.134.249:/yoyo/apps/
echo ">>证书远端连接服务器 请输入密码:"
ssh -p 6000 -i /Users/topbrids/cert/yyb.pem root@49.233.134.249

# echo ">>进入到服务器项目发布路径"
# cd /apps/project/lfex

# echo ">>解压压缩包到当前路径"
# tar -zxvf lfexapi_dev.tar.gz

