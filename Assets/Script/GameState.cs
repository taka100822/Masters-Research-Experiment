public enum GameState
{
    FreeMove,
    InDialogue,
    InChoice,
    InTyping,  // ★追加
    Ended,     // 時間切れ後の終了画面（操作不可）
    InSettings, // 設定パネル表示中（時間停止）
    Intro      // 探索前の説明画面（README 11.2）
}
