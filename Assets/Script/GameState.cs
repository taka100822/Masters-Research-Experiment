public enum GameState
{
    FreeMove,
    InDialogue,
    InChoice,
    InTyping,  // ★追加
    Ended,     // クエスト達成後の終了画面（操作不可）
    InSettings // 設定パネル表示中（時間停止）
}