# Requirements Document

## Project Description (Input)
Unity の Timeline を自動構築する UPM パッケージツール。開発配置はリポジトリ内 Unity プロジェクト UnityTimelineBuilder(Unity 6000.0.36f1、com.unity.timeline 1.8.13)の Packages/com.hidano.unity-timeline-builder/ に embedded package として実装する。

【目的】外部から構築情報(CSV/TSV)とリソースファイルのパスを受け取り、任意の Unity プロジェクト内に TimelineAsset と、それを再生する PlayableDirector を持つ Prefab を自動構築する。

【入力】
- 構築情報フォーマットは CSV/TSV のみ(Google スプレッドシートで作成し File > Download でエクスポートしたものを直接パースする。JSON/XML は将来拡張であり今回スコープ外)
- 構築情報の内容: Timeline Track の種類、Track 名、クリップの StartTime、ClipIn、Duration、Clip 名、参照リソースのパス
- Google スプレッドシートにインポートできるデータ構造テンプレート(CSV テンプレートファイルと列定義ドキュメント)を成果物として定義・同梱する

【リソース】
- 初期対応は AudioClip(wav, mp3)と AnimationClip(fbx 内包含む)のみ。リソース種別は今後拡張される前提の設計にする
- リソースパスは両対応: Assets/ 配下の既存アセットパスならそのまま参照し、プロジェクト外の絶対/相対パスならプロジェクトへコピーしてインポート後に参照する

【出力】
- TimelineAsset(AudioTrack / AnimationTrack とクリップ配置済み)
- PlayableDirector を持つ GameObject の Prefab(playableAsset に TimelineAsset を設定)
- トラックバインディング(Animator / AudioSource 等の割り当て)は設定しない。バインドは利用者が後から手動で行う

【実行形態】
- コマンドラインからのバッチ実行を想定(Unity Editor の -batchmode / -executeMethod 経由)
- 公開 API は public static メソッドとして提供し、CLI からもエディタ拡張からも呼べるようにする
- バッチ実行時のエラーは exit code とログで判別可能にする

## Introduction
本仕様は、CSV/TSV 形式の構築情報とリソースファイルのパスを入力として、Unity プロジェクト内に TimelineAsset と PlayableDirector 付き Prefab を自動構築する UPM パッケージ「Unity Timeline Builder」の要件を定義する。パッケージは Unity プロジェクト UnityTimelineBuilder(Unity 6000.0.36f1、com.unity.timeline 1.8.13)の embedded package `com.hidano.unity-timeline-builder` として実装し、public static API と Unity Editor のバッチモード(-batchmode / -executeMethod)経由の CLI 実行の両方をサポートする。

## Boundary Context

- **In scope**:
  - CSV/TSV 構築情報のパースとバリデーション
  - AudioTrack / AnimationTrack と各クリップ(AudioClip: wav・mp3、AnimationClip: .anim および fbx 内包)の配置
  - プロジェクト外リソースのコピー・インポートと Assets/ 配下既存アセットの参照
  - TimelineAsset と PlayableDirector 付き Prefab の生成
  - public static API と CLI バッチ実行(exit code・ログによる結果判別)
  - Google スプレッドシートにインポート可能な CSV テンプレートと列定義ドキュメントの同梱
- **Out of scope**:
  - JSON/XML など CSV/TSV 以外の構築情報フォーマット(将来拡張)
  - AudioTrack / AnimationTrack 以外のトラック種別(将来拡張。ただし拡張可能な設計とする)
  - トラックバインディング(Animator / AudioSource 等の割り当て)の設定
  - ランタイム(再生時)での Timeline 構築
- **Adjacent expectations**:
  - 生成された Prefab のトラックバインディングは利用者が手動で設定する
  - 構築情報は Google スプレッドシートで作成し、File > Download でエクスポートした CSV/TSV をそのまま入力とする

## Requirements

### Requirement 1: 構築情報(CSV/TSV)のパース
**Objective:** As a コンテンツ制作者, I want Google スプレッドシートからエクスポートした CSV/TSV をそのまま構築情報として読み込ませたい, so that スプレッドシート上で管理している演出データから追加の変換作業なしに Timeline を構築できる

#### Acceptance Criteria
1. When 構築情報ファイルのパスが指定されたとき, the Unity Timeline Builder shall 拡張子(.csv / .tsv)に応じて CSV またはタブ区切り(TSV)としてファイルをパースする
2. The Unity Timeline Builder shall Google スプレッドシートの File > Download でエクスポートされた CSV/TSV(ダブルクォート囲み、フィールド内のカンマ・改行・エスケープされた引用符を含む)を正しくパースする
3. The Unity Timeline Builder shall 構築情報の各行から Track の種類、Track 名、クリップの StartTime、ClipIn、Duration、Clip 名、参照リソースのパスを読み取る
4. When 構築情報にヘッダー行が含まれるとき, the Unity Timeline Builder shall ヘッダー行を列定義として認識し、データ行として扱わない
5. If 指定された構築情報ファイルが存在しない、または読み取れないとき, then the Unity Timeline Builder shall 対象パスを含むエラーを報告し、構築処理を中断する
6. If 構築情報に必須列の欠落、未対応の Track 種類、または数値項目(StartTime / ClipIn / Duration)として解釈できない値が含まれるとき, then the Unity Timeline Builder shall 行番号と原因を特定できるエラーを報告し、構築処理を中断する

### Requirement 2: リソース解決とインポート
**Objective:** As a コンテンツ制作者, I want プロジェクト内の既存アセットとプロジェクト外のリソースファイルの両方をクリップの参照先として指定したい, so that リソースの置き場所を意識せずに構築情報を記述できる

#### Acceptance Criteria
1. When 参照リソースのパスが Assets/ 配下のアセットパスであるとき, the Unity Timeline Builder shall そのアセットをコピーせず既存アセットとして参照する
2. When 参照リソースのパスがプロジェクト外の絶対パスまたは相対パスであるとき, the Unity Timeline Builder shall リソースファイルをプロジェクト内の所定のインポート先へコピーし、インポート後のアセットを参照する
3. The Unity Timeline Builder shall AudioClip として wav および mp3 形式のリソースを解決する
4. The Unity Timeline Builder shall AnimationClip として単体アセットおよび fbx に内包された AnimationClip を解決する
5. When fbx に複数の AnimationClip が内包されているとき, the Unity Timeline Builder shall 構築情報で指定された名前に一致する AnimationClip を選択して参照する
6. If 参照リソースのパスに対応するファイルまたはアセットが存在しないとき, then the Unity Timeline Builder shall 対象の行と参照パスを特定できるエラーを報告し、構築処理を中断する
7. If 解決したリソースの種別が対象トラックの種類と一致しないとき, then the Unity Timeline Builder shall 対象の行と不一致の内容を特定できるエラーを報告し、構築処理を中断する
8. The Unity Timeline Builder shall リソース種別ごとの解決処理を、将来のリソース種別追加時に既存処理を変更せずに拡張できる構造で提供する

### Requirement 3: TimelineAsset の構築
**Objective:** As a コンテンツ制作者, I want 構築情報どおりにトラックとクリップが配置された TimelineAsset を自動生成したい, so that Unity Editor 上での手作業によるクリップ配置をなくせる

#### Acceptance Criteria
1. When 構築処理が実行されたとき, the Unity Timeline Builder shall TimelineAsset を生成し、指定された出力先にアセットとして保存する
2. The Unity Timeline Builder shall 構築情報で指定された Track の種類(AudioTrack / AnimationTrack)と Track 名で TimelineAsset にトラックを作成する
3. When 構築情報の複数行が同一の Track 名と Track の種類を持つとき, the Unity Timeline Builder shall それらのクリップを同一トラック上に配置する
4. The Unity Timeline Builder shall 各クリップに構築情報で指定された StartTime、ClipIn、Duration、Clip 名を設定し、解決済みリソース(AudioClip / AnimationClip)を割り当てる
5. The Unity Timeline Builder shall 構築情報に記載された行の内容をトラックおよびクリップへ過不足なく反映する(記載されていないトラック・クリップを生成しない)
6. The Unity Timeline Builder shall トラック種別ごとの構築処理を、将来のトラック種別追加時に既存処理を変更せずに拡張できる構造で提供する

### Requirement 4: Prefab の構築
**Objective:** As a コンテンツ制作者, I want 生成した TimelineAsset を再生する PlayableDirector 付き Prefab を自動生成したい, so that 生成物をシーンへ配置するだけで Timeline を再生する準備が整う

#### Acceptance Criteria
1. When TimelineAsset の構築が完了したとき, the Unity Timeline Builder shall PlayableDirector コンポーネントを持つ GameObject の Prefab を生成し、指定された出力先にアセットとして保存する
2. The Unity Timeline Builder shall 生成した Prefab の PlayableDirector の playableAsset に、構築した TimelineAsset を設定する
3. The Unity Timeline Builder shall PlayableDirector のトラックバインディング(Animator / AudioSource 等の割り当て)を設定しない
4. If 出力先パスに既存のアセットが存在するとき, then the Unity Timeline Builder shall 既存アセットを上書きし、上書きした旨をログに出力する

### Requirement 5: 公開 API
**Objective:** As a ツール開発者, I want 構築処理を public static メソッドとして呼び出したい, so that CLI・エディタ拡張・他ツールのいずれからも同一の処理を再利用できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall 構築情報ファイルのパスと出力先を入力として Timeline と Prefab を構築する public static メソッドを公開する
2. When 公開 API が呼び出されたとき, the Unity Timeline Builder shall 構築の成功・失敗および失敗理由を呼び出し元が判別できる形式で返却する
3. If 公開 API に不正な引数(null、空文字、存在しないパス等)が渡されたとき, then the Unity Timeline Builder shall 例外送出またはエラー結果の返却により原因を特定できる形で失敗を通知する
4. The Unity Timeline Builder shall 公開 API を Unity Editor 環境で動作するエディタ用アセンブリとして提供する

### Requirement 6: CLI バッチ実行
**Objective:** As a ビルドパイプライン管理者, I want Unity Editor の -batchmode / -executeMethod 経由で構築処理を実行したい, so that CI や外部スクリプトから人手を介さずに Timeline を構築できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall Unity Editor の -batchmode / -executeMethod から呼び出せるエントリポイントを提供する
2. When CLI エントリポイントが実行されたとき, the Unity Timeline Builder shall コマンドライン引数から構築情報ファイルのパスと出力先の指定を読み取る
3. When 構築処理が正常に完了したとき, the Unity Timeline Builder shall exit code 0 で Unity Editor プロセスを終了させる
4. If 構築処理が失敗したとき, then the Unity Timeline Builder shall 0 以外の exit code で Unity Editor プロセスを終了させる
5. If 構築処理が失敗したとき, then the Unity Timeline Builder shall 失敗の原因(対象ファイル、行番号、エラー内容等)を特定できるメッセージをログに出力する
6. When 構築処理が正常に完了したとき, the Unity Timeline Builder shall 生成した TimelineAsset と Prefab のアセットパスをログに出力する

### Requirement 7: データ構造テンプレートとドキュメント
**Objective:** As a コンテンツ制作者, I want Google スプレッドシートにインポートできる CSV テンプレートと列定義ドキュメントを参照したい, so that 正しいデータ構造で構築情報を作成できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall Google スプレッドシートにインポート可能な CSV テンプレートファイルをパッケージ成果物として同梱する
2. The Unity Timeline Builder shall CSV テンプレートに、パーサーが認識するヘッダー行と各列の記入例を示すサンプルデータ行を含める
3. The Unity Timeline Builder shall 各列の名称・意味・データ型・必須/任意・記入例を記載した列定義ドキュメントをパッケージ成果物として同梱する
4. The Unity Timeline Builder shall CSV テンプレートおよび列定義ドキュメントの内容をパーサーの仕様と一致させる

### Requirement 8: UPM パッケージ構成
**Objective:** As a ツール開発者, I want 本ツールを UPM パッケージとして配置・配布したい, so that 任意の Unity プロジェクトへ導入して利用できる

#### Acceptance Criteria
1. The Unity Timeline Builder shall Unity プロジェクト UnityTimelineBuilder の Packages/com.hidano.unity-timeline-builder/ に embedded package として配置される
2. The Unity Timeline Builder shall UPM の規約に従った package.json(パッケージ名 com.hidano.unity-timeline-builder)およびアセンブリ定義を含む
3. The Unity Timeline Builder shall com.unity.timeline(1.8.13)への依存を package.json に宣言する
4. The Unity Timeline Builder shall Unity 6000.0.36f1 上で動作する
