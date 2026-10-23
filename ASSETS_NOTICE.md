# 素材来源

本项目为娱乐用途的桌面宠物。第三方角色、原始图像、图标、动画和音频权利归相应权利人；本说明记录来源，不改变原素材的授权状态。

| 内容 | 来源与固定提交 | 处理 |
| --- | --- | --- |
| 待机、大笑、挥手、睡觉 PNG，ICO 图标及笑声源文件 | [woainiunai/yellow-belly-creature-desktop](https://github.com/woainiunai/yellow-belly-creature-desktop/tree/45857f860a221a4af1fd2469d658cc7ad8b4462e) | 保留角色像素；源 `laugh.mp3` 实际为含视频和 AAC 音轨的 MOV，提取声音并转换为 PCM WAV |
| 嘎嘎滴辣虾、安迪、噶脚塞、加油 | [Diyeego/naiwa-pet / naiwa/voice](https://github.com/Diyeego/naiwa-pet/tree/1827c36d2abdca7935858ff11d37f50ec49d13af/naiwa/voice) | 对应 `gagadilaxia.mp3`、`andi (1).mp3`、`gajiaosei.mp3`、`ganbadie.mp3`，转换为 44.1 kHz、16 位、双声道 PCM WAV，并统一响度 |
| 打拳、八卦动作 | [lyhkevin/Codex_pet_naiwa / gifs/extras](https://github.com/lyhkevin/Codex_pet_naiwa/tree/aa6dc78ab2c623d28274b232101cc1270e928204/gifs/extras) | 从 `punching.gif`、`gossiping.gif` 提取 8 帧透明 PNG；每个动作使用同一缩放和基线变换 |

本仓库的 C# / WPF 桌面窗口、事件处理、音频播放与测试为本次项目编写。没有运行或捆绑上述仓库的成品桌宠程序。

素材转换阶段使用 `imageio-ffmpeg` 0.6.0 包中的 FFmpeg；运行桌宠不依赖该工具。与公开素材有关的署名、更正或移除请求可通过本仓库 Issue 提交。
