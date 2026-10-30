# 話題セット

両セットの話題のつながりと対応、会話のセリフとAIのプロンプトのある場所。仕組み（会話の型・テンプレート・置き場所）はREADMEの4.4・4.5・5.3、誰がどこにいるかは5.2。

* **セリフの全文はここには書かない**。[スプレッドシート「NPC会話テキスト」](https://docs.google.com/spreadsheets/d/1UrnokKpW8CRvu7GmQE8EDhN05eMvUk8BzUUhgON8yIs/edit)で編集し、Unityに取り込む（README 4.8）
* **プロンプトの全文はここには書かない**。[スプレッドシート「NPCプロンプト」](https://docs.google.com/spreadsheets/d/1DpBEnJ_7bN44l1gGB1NgiLCCUcDbHpGMiggjoKVDg7M/edit)で編集し、Unityに取り込む（README 4.7）
* 研究仕様（README 1.6）なので、変えるときは研究への影響を確認してから

## 目次

1. [話題セット1](#1-話題セット1)
2. [話題セット2](#2-話題セット2)
3. [2つのセットの対応](#3-2つのセットの対応)
4. [チュートリアル](#4-チュートリアル)

---

## 1. 話題セット1

**話題2のつながり**：ヴィクター → ステフ → ティム → ヤニス → ニコラ → ルカ → ヴィクター（矢印の先が、その人の話題2で紹介される人）

### 会話（`Resources/dialogue/S1/`）

全文は[スプレッドシート「NPC会話テキスト」](https://docs.google.com/spreadsheets/d/1UrnokKpW8CRvu7GmQE8EDhN05eMvUk8BzUUhgON8yIs/edit)の`dialogue`タブ（folderが`S1`の行）にある。シートが正本で、Unityには **Tools → 研究用 → 会話テキストをスプレッドシートから取り込む** で入れる（README 4.8）。実験で使った版は、gitのCSVに残る。選択肢・遷移・`action`はREADME 5.3の型のとおり（全員同じ）。

### プロンプト（`Resources/PromptData/S1/`）

全文は[スプレッドシート「NPCプロンプト」](https://docs.google.com/spreadsheets/d/1DpBEnJ_7bN44l1gGB1NgiLCCUcDbHpGMiggjoKVDg7M/edit)の`prompts`タブ（folderが`S1`の行）にある。シートが正本で、Unityには **Tools → 研究用 → プロンプトをスプレッドシートから取り込む** で入れる（README 4.7）。実験で使った版は、gitの`PromptData`アセットに残る。

---

## 2. 話題セット2

構成はセット1と同じ。

**話題2のつながり**：シャック → カワイ → マヌ → マイケル → パウ → ディルク → シャック

### 会話（`Resources/dialogue/S2/`）

全文は[スプレッドシート「NPC会話テキスト」](https://docs.google.com/spreadsheets/d/1UrnokKpW8CRvu7GmQE8EDhN05eMvUk8BzUUhgON8yIs/edit)の`dialogue`タブ（folderが`S2`の行）にある。シートが正本で、Unityには **Tools → 研究用 → 会話テキストをスプレッドシートから取り込む** で入れる（README 4.8）。実験で使った版は、gitのCSVに残る。

### プロンプト（`Resources/PromptData/S2/`）

全文は[スプレッドシート「NPCプロンプト」](https://docs.google.com/spreadsheets/d/1DpBEnJ_7bN44l1gGB1NgiLCCUcDbHpGMiggjoKVDg7M/edit)の`prompts`タブ（folderが`S2`の行）にある。シートが正本で、Unityには **Tools → 研究用 → プロンプトをスプレッドシートから取り込む** で入れる（README 4.7）。実験で使った版は、gitの`PromptData`アセットに残る。

---

## 3. 2つのセットの対応

| | セット1 | セット2 |
|---|---|---|
| 人数・位置 | 6人（NPC001〜006の位置） | 同じ |
| 衣装 | 商人・平民・薬師・貴族・鍛冶屋・料理人 | 同じ6種類を、どの位置でもセット1と違うように入れ替える |
| 話題 | 1人2つ（仕事のこと・村の人のこと）、各2ノード | 同じ |
| 話題2のつながり | 6人で1つの輪（全員が1回ずつ紹介される） | 同じ |
| 話題の調子 | 村の暮らしの軽い話（事件や謎はない） | 同じ |
| 自由入力（条件B） | 6人とも、あいさつのあとは自由入力だけ（README 1.8） | 同じ |

* 話題2では、紹介する人の**居場所**（「東の桟橋の手前」など）を必ず言う。ミニマップと合わせて、次に話す人を探せる
* プロンプトの【知っている情報】には、セリフの内容に加えて、**自分を紹介している人とのつながり**も書いた（例：ヤニスは、ティムが道具を直してもらったと話す相手なので、「ティムの道具を直した」ことを知っている）。自由入力で「〇〇さんがあなたのことを話していたよ」と聞かれても話が食い違わないようにするため。セリフ（選択肢で聞ける内容）には出てこない情報だが、相手のセリフで既に話されている事実の裏返しなので、新しい話題にはならない
* セット1はクエスト版（docs/history.md）と名前・性格は同じだが、会話もプロンプトも新しく書いたもの。クエスト版の手がかり（薬草・灯篭）は出てこない（ニコラの収穫祭の灯篭は飾る話だけで、なくなってはいない）

---

## 4. チュートリアル

案内役の2人（README 2.4）。話題セットとは別で、両セット・両条件で同じ。村・村の人たちには触れない。

### 会話（`Resources/dialogue/Tutorial/`）

全文は[スプレッドシート「NPC会話テキスト」](https://docs.google.com/spreadsheets/d/1UrnokKpW8CRvu7GmQE8EDhN05eMvUk8BzUUhgON8yIs/edit)の`dialogue`タブ（folderが`Tutorial`の行）にある。シートが正本で、Unityには **Tools → 研究用 → 会話テキストをスプレッドシートから取り込む** で入れる（README 4.8）。実験で使った版は、gitのCSVに残る。

選択肢・遷移はどちらも同じ：`0` 話を聞く → `10` ／ なんでもない → `90`、`10` 2つの選択肢 → `20`・`30`、`20`・`30` → `99`、`90`・`99` で終わり。`action`はなし。

### プロンプト（`Resources/PromptData/Tutorial/`）

全文は[スプレッドシート「NPCプロンプト」](https://docs.google.com/spreadsheets/d/1DpBEnJ_7bN44l1gGB1NgiLCCUcDbHpGMiggjoKVDg7M/edit)の`prompts`タブ（folderが`Tutorial`の行）にある。シートが正本で、Unityには **Tools → 研究用 → プロンプトをスプレッドシートから取り込む** で入れる（README 4.7）。実験で使った版は、gitの`PromptData`アセットに残る。
