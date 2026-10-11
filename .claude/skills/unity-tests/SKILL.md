---
name: unity-tests
description: Run Unity Edit Mode and Play Mode tests, through the Unity MCP server when the editor is open or in batch mode when it is closed, and summarise failures.
argument-hint: "[editmode | playmode | all]"
---

Platform: $ARGUMENTS (empty means all)

1. **Editor open** (the Unity MCP server responds): run the tests through its test-runner tool, then read the console for compile errors first.
2. **Editor closed:** run batch mode from the repository root, once per platform:
   `"<Unity 6.3 editor>" -batchmode -nographics -projectPath unity -runTests -testPlatform EditMode -testResults TestResults/editmode.xml -logFile TestResults/editmode.log`
   (Play Mode: `-testPlatform PlayMode`, and drop `-nographics` for rendering tests). On Windows the editor is usually under `C:\Program Files\Unity\Hub\Editor\6000.3.*\Editor\Unity.exe`.
3. Parse the NUnit XML results: totals, then each failure with its message and the first stack frame in our code.
4. If compilation failed, report the first compiler errors from the log instead; nothing else is meaningful until they are fixed.
5. Remind: gameplay bugs belong in the simulation tests; Unity tests cover the bridge, tools and the picture.
