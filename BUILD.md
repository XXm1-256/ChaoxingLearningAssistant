# v1.38 构建说明

默认入口：`BUILD_V1_38.bat`。直接构建并启动可运行 `START.bat`；完整便携包/安装器使用 `BUILD_FULL.bat`。

当前程序版本：v1.38 / 1.38.0。

## Windows 快速验证

1. 完整解压 ZIP 到新目录，不覆盖旧版本。
2. 双击 `BUILD_V1_38.bat`。
3. 构建结果写入根目录 `BUILD_LOG.txt`。
4. 成功后启动 `artifacts\quick-run\ChaoxingLearningAssistant.exe`。
5. 进入真实课程视频，确认右侧课程 / 章节 / 视频与学习通实际内容一致。
6. 单击左侧不同章节，确认学习通实际播放器先切换，随后左右栏同步。
7. 选择有多个视频任务的章节，确认“打开未完成”只处理平台明确标记未完成的任务；状态未知不能被当成未完成。
8. 使用常用倍速（尤其 2x）让视频自然结束，确认学习通网页真实进入下一视频。
9. 若失败，提交 `BUILD_LOG.txt` 和运行日志中 `CX-VIDEO-TASK-SCAN / CX-PLAYER-SWITCH / CX-MANUAL-CHAPTER* / CX-AUTO-NEXT* / CX-CHAPTER*` 相关行。

`START.bat` 会检查 `artifacts\QUICK_RUN_READY.txt` 中的 `Version: 1.38.0`，版本不匹配时自动重新构建。

Windows 编译与测试记录位于 `docs/validation/v1.38/`。真实学习通课程仍需实机账号环境验收。
