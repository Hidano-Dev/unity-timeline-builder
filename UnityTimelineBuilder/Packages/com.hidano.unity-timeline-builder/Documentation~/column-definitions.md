# Timeline 構築情報 CSV 列定義

このドキュメントと `timeline-template.csv` は、構築情報 CSV の列仕様を共有します。CSV の1行目は必ず次のヘッダー行にしてください。Google スプレッドシートでは **File > Import** から CSV を読み込めます。

## 列仕様

| 列名 | 意味 | データ型 | 必須/任意 | 記入例 |
|---|---|---|---|---|
| `trackType` | トラック種別。登録済みキーを指定します。 | string（`Audio` / `Animation`） | 必須 | `Audio` |
| `trackName` | トラック名。同じ種別・同じ名前の行は同一トラックに集約されます。 | string | 必須 | `BGM` |
| `clipName` | クリップ表示名。FBX に複数の AnimationClip が含まれる場合はサブアセット選択キーになります。空欄の場合は解決されたアセット名（FBX は内包クリップ名）を使用します。 | string | 任意 | `intro` |
| `startTime` | クリップの開始時刻（秒）。0以上を指定します。 | double（`>= 0`） | 必須 | `0.5` |
| `clipIn` | クリップ内オフセット（秒）。0以上を指定します。 | double（`>= 0`） | 必須 | `0` |
| `duration` | クリップの長さ（秒）。指定する場合は0より大きい値。空欄の場合はアセットの長さいっぱいになります。 | double（`> 0`） | 任意 | `3.2` |
| `resourcePath` | 参照する AudioClip または AnimationClip のパス。 | string | 必須 | `Assets/Audio/intro.wav` |

`clipName` と `duration` は列自体を省略することもできます（ヘッダー行がある場合）。ヘッダー行を省略する場合は既定の 7 列順で解釈されるため、空欄セルとして残してください。

## Scene 行の列仕様

`trackType` が `Scene`、`ScenePrefab`、`SceneBind` の行は、Timeline のクリップ行とは異なる Scene 構築情報として解釈されます。ヘッダー行を省略する場合も、次の固定 7 列順を使用してください。各行の未使用列は空欄にします。

| 行種別 (`trackType`) | `trackName` | `clipName` | `startTime` / `clipIn` / `duration` | `resourcePath` |
|---|---|---|---|---|
| `Scene` | Scene 名。生成する `.unity` ファイル名に使用します。 | 未使用 | 未使用 | 割り当てる TimelineAsset。空欄なら同一ビルドで生成した TimelineAsset、`Assets/` で始まる場合は既存 TimelineAsset のパスです。 |
| `ScenePrefab` | 未使用 | 未使用 | 未使用 | Scene に配置する Prefab のアセットパス。`Assets/` 配下を指定します。 |
| `SceneBind` | バインド先 AnimationTrack の Track 名。クリップ行の `trackName` と一致させます。 | 未使用 | 未使用 | バインド対象の GameObject 名。Scene 内で一意である必要があります。 |

| 行種別 | データ型 | 必須/任意 | 記入例 |
|---|---|---|---|
| `Scene` の `trackName` | string（ファイル名として有効） | 必須 | `SampleScene` |
| `Scene` の `resourcePath` | string（空欄または `Assets/` パス） | 任意 | `Assets/Timelines/Existing.playable` |
| `ScenePrefab` の `resourcePath` | string（`Assets/` 配下の Prefab パス） | 必須 | `Assets/Prefabs/Character.prefab` |
| `SceneBind` の `trackName` | string（Ordinal 完全一致） | 必須 | `Character` |
| `SceneBind` の `resourcePath` | string（GameObject 名） | 必須 | `CharacterRoot` |

1シートにつき `Scene` 行は1行まで指定できます。`ScenePrefab` または `SceneBind` 行を使用する場合は `Scene` 行も必要です。Prefab は行の出現順にすべて配置されます。

### Scene の参照・名前解決規約

- `Scene` の `resourcePath` が空欄の場合、同一ビルドで生成した TimelineAsset を PlayableDirector に割り当てます。`Assets/` で始まる場合は既存 TimelineAsset を参照します。
- `ScenePrefab` の `resourcePath` は `Assets/` 配下の Prefab アセットパスでなければなりません。
- `SceneBind` の GameObject 名は、非アクティブな GameObject も含めて Scene 全体から Ordinal（大文字小文字を区別する）完全一致で探索します。
- 同名の GameObject が複数ある場合、対象を一意に決定できないためエラーになります。対象 GameObject には Animator コンポーネントが必要です。
- `SceneBind` は Track 名で AnimationTrack を指定します。バインド指定のない Track は未設定のまま保持されます。

## `resourcePath` の規約

`resourcePath` は `trackType` に対応するリソースを指定します。

| `trackType` | 指定できるリソース | パスの規約 |
|---|---|---|
| `Audio` | `AudioClip` | `.wav` または `.mp3`。`Assets/` で始まる場合はプロジェクト内の既存アセットを参照し、それ以外は外部ファイルとして取り込みます。 |
| `Animation` | `AnimationClip` | `.anim` または `.fbx`。FBX に複数のクリップがある場合は `clipName` と名前が一致するクリップを選択します。`Assets/` で始まる場合はプロジェクト内の既存アセットを参照し、それ以外は外部ファイルとして取り込みます。 |

パスは Unity プロジェクトの `Assets/` を基準にしたパス、または外部ファイルの絶対パス・相対パス（シートファイルのあるディレクトリ基準）で指定します。外部ファイルはビルド時にインポート先（既定 `Assets/UnityTimelineBuilder/Imported`）へ自動コピーされます。エクスプローラーの「パスのコピー」等で付くダブルクォート囲み（`"C:\...\intro.wav"`）はそのまま貼り付けても解釈されます。外部ファイルを使う場合は、構築 API のインポート先が `Assets/` 配下である必要があります。

## 記入例

```csv
trackType,trackName,clipName,startTime,clipIn,duration,resourcePath
Audio,BGM,intro,0,0,3.2,Assets/Audio/intro.wav
Animation,Character,intro,0.5,0,2.5,Assets/Animations/character.fbx
Scene,SampleScene,,,,,
ScenePrefab,,,,,,Assets/Prefabs/Character.prefab
SceneBind,Character,,,,,CharacterRoot
```

数値は秒単位の小数で記入します。CSV の値にカンマや改行を含める場合は、RFC 4180 に従って値全体をダブルクォートで囲み、値中のダブルクォートは `""` としてエスケープしてください。
