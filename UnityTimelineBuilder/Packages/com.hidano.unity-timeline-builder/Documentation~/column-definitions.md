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
| `timeline` | 行を所属させる Timeline グループ名。生成する TimelineAsset / Prefab のアセット名にも使用されます。 | string | ヘッダーがある場合は必須（従来形式では任意） | `Opening` |

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

## 複数 Timeline の記入

`timeline` 列は、ヘッダー行で列名が認識された場合にだけ有効です。列名の大文字小文字は区別せず、列の位置は自由です。ヘッダーがない従来形式では `timeline` 列は認識されず、既定の7列を使う単一 Timeline として解釈されます。

`timeline` 列を使用する場合、クリップ行だけでなく `Scene`、`ScenePrefab`、`SceneBind` 行にも Timeline 名を必ず記入してください。値は前後の空白をトリムしてから、Ordinal（大文字小文字を区別する）完全一致でグループ化します。同じ名前の行はシート上で連続していなくても同じグループに集約され、出力グループの順序は各名前が最初に現れた順になります。大文字小文字だけが異なる名前は別グループです。

各グループにはクリップ行を置き、`ScenePrefab` または `SceneBind` を置く場合は同じグループに `Scene` 行を1行置いてください。`Scene` 行はグループごとに最大1行です。Scene 行の `resourcePath` が空欄なら同じグループで生成した TimelineAsset を参照し、`Assets/` パスなら既存アセットを参照します。

`timeline` 列を使用するシートでは `AssetName`（API の `BuildRequest.AssetName`、CLI の `-assetName`）を指定できません。Timeline 名が各グループのアセット名になるためです。指定した場合は `AssetNameConflict` ビルドエラーになります。Timeline 名はファイル名として有効な文字列にし、制御文字、`<>:"|?*`、末尾のピリオド・空白、`.`、`..` は使用しないでください。

### 名前のパス・拡張子の自動正規化

`timeline` 列・`Scene` 行の Scene 名・`AssetName`（API / CLI）に区切り文字（`/` または `\`）を含む値を指定した場合、パスが入力されたものと解釈し、最後の区切り文字から末尾までをファイル名として採用します。さらに、末尾が既知の拡張子（`.playable` / `.prefab` / `.unity` / `.asset` / `.csv`、大文字小文字は区別しない）の場合は拡張子を除去します。例: `Assets/Timelines/Opening.playable` → `Opening`。この正規化はグループ化の前に行われるため、`Opening` と `Assets/Timelines/Opening.playable` は同じグループになります。既知の拡張子でないドット入りの名前（例: `Ver1.5`）はそのまま保持されます。正規化後に名前が空になる値（例: `Assets/`）はエラーです。

同一ビルド内で出力フォルダ名が大文字小文字を区別せず衝突する場合、後のグループへ ` (1)`、` (2)` のような連番サフィックスを付けてリネームします。リネーム時は元の名前と確定した出力フォルダを警告ログに出力し、Prefab と TimelineAsset のペアは同じ確定名を使用します。`timeline` は予約列名のため、必ずヘッダーに列名を追加してください。

## 生成アセットの出力フォルダ構成

グループごとに出力ディレクトリ直下へフォルダを 1 つ作成し、成果物を種類別のサブフォルダへ配置します。フォルダ名は Scene 行を持つグループでは **Scene 名**、持たないグループでは **アセット名（Timeline 名）** です。

```
<出力ディレクトリ>/
  <Scene名 または アセット名>/
    Scenes/     <Scene名>.unity        （Scene 行を持つグループのみ）
    Timelines/  <アセット名>.playable
    Prefabs/    <アセット名>.prefab
    AudioClips/ 外部から取り込んだ AudioClip
    Animations/ 外部から取り込んだ AnimationClip / FBX
```

- 衝突判定の単位はフォルダ名です。大文字小文字を区別せず同名になる場合、後のグループのフォルダ名（およびその由来である Scene 名またはアセット名）へ連番サフィックスが付きます。
- 外部ファイル（`Assets/` で始まらない `resourcePath`）は、参照したグループのフォルダ配下へ取り込まれます。複数のグループが同じ外部ファイルを参照する場合は**各グループのフォルダへそれぞれ複製**され、フォルダ単位で自己完結します（カットフォルダを UPM パッケージとして単独配布する運用を想定）。取り込み先の直下にはソースディレクトリごとの `src_<ハッシュ>` フォルダが作られ、同名ファイルの衝突を防ぎます。
- `resourcePath` が `Assets/...` の既存アセットを参照している場合は、移動もコピーもしません。
- API の `BuildRequest.ImportDirectory` または CLI の `-importDir` を明示指定した場合は、従来どおり外部取り込みをそのディレクトリへ集約します（グループフォルダへの複製は行いません）。
