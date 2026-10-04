public enum GameState
{
    FreeMove,
    InDialogue,
    InChoice,
    InTyping,  // ★追加
    Ended      // クエスト達成後の終了画面（操作不可）
}