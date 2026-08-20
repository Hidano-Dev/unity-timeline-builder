# Implementation Plan

- [ ] 1. Foundation: embedded package の骨組みを作成する
- [x] 1.1 UPM パッケージの配置とマニフェストを整備する
  - `UnityTimelineBuilder/Packages/com.hidano.unity-timeline-builder/` に embedded package のディレクトリ構成(Editor / Documentation~ / Tests)を作成する
  - package.json にパッケージ名 `com.hidano.unity-timeline-builder`、Unity バージョン `6000.0`、`com.unity.timeline` 1.8.13 への依存を宣言する
  - README.md / CHANGELOG.md / LICENSE.md の初期ファイルを配置する
  - Unity Editor がパッケージを embedded package として認識し、Package Manager 上に表示されることを確認できる
  - _Requirements: 8.1, 8.2, 8.3, 8.4_

- [ ] 1.2 Editor 専用アセンブリ定義とテスト用アセンブリ定義を作成する
  - 本体 asmdef を Editor プラットフォーム限定とし、Unity.Timeline への参照を設定する
  - テスト用 asmdef に本体・TestRunner 参照と `UNITY_INCLUDE_TESTS` の defineConstraints を設定する(非推奨の optionalUnityReferences は使わない)
  - 空のアセンブリ 2 つがコンパイルエラーなくビルドされ、Test Runner に EditMode テストアセンブリとして認識されることを確認できる
  - _Requirements: 5.4, 8.2_

- [x] 2. 構築ジョブの共有データモデルを定義する
  - 構築要求(入力パス・出力先・アセット名・インポート先)、構築結果(成否・出力パス・エラー一覧)、エラー詳細(エラーコード・行番号・対象パス・メッセージ)を公開契約として定義する
  - 構築情報 1 行の型付き表現(行番号、トラック種別・トラック名・クリップ名・StartTime・ClipIn・Duration・リソースパス)を不変データとして定義する
  - エラーコード列挙が設計のエラー分類(引数不正・シート不在・パース失敗・行検証・未知トラック種別・リソース不在・種別不一致・インポート失敗・出力失敗・予期しない例外)を網羅している
  - モデルのみでアセンブリがコンパイルされ、後続レイヤーが参照できる状態になっている
  - _Requirements: 1.3, 5.2_

- [ ] 3. Parsing: 構築情報の読み取りと検証を実装する
- [ ] 3.1 (P) CSV/TSV リーダーを実装する
  - 拡張子 .csv / .tsv に応じてカンマ / タブ区切りを自動判別し、未対応拡張子はエラーにする
  - ダブルクォート囲みフィールド内のカンマ・改行・エスケープ引用符(`""`)を RFC 4180 準拠で復元し、UTF-8 BOM 有無と CRLF/LF 混在を許容する
  - ファイル不在・読取不能時は対象パスを含むエラーで失敗する
  - Google スプレッドシートエクスポート相当のクォート・改行・BOM・TSV を網羅した単体テストがすべてパスする
  - タスク 2 のモデルには依存しない純粋な .NET 実装のため、タスク 2 と並行可能
  - _Requirements: 1.1, 1.2, 1.5_
  - _Boundary: CsvSheetReader_
  - _Depends: 1.2_

- [ ] 3.2 構築情報パーサー(ヘッダー認識・列マッピング・行検証)を実装する
  - 先頭行のヘッダー認識(大文字小文字無視・列順自由)と、ヘッダー未検出時の既定列順フォールバック + Warning ログを実装する
  - 各データ行から 7 項目(トラック種別・トラック名・クリップ名・StartTime・ClipIn・Duration・リソースパス)を型付き行へ変換する(数値は InvariantCulture で解釈)
  - 必須列欠落・未対応トラック種別・数値解釈不能を、元ファイル 1 始まりの行番号付きエラーとして最終行まで収集する(トラック種別の妥当性判定は外部注入の判定関数で行い、Building 層へ依存しない)
  - ヘッダー有無・列順シャッフル・各種検証エラーの行番号を検証する単体テストがすべてパスする
  - _Requirements: 1.3, 1.4, 1.6, 7.4_

- [ ] 4. Resources: リソース解決とインポートを実装する
- [ ] 4.1 (P) 外部ファイルのコピー・同期インポート処理を実装する
  - プロジェクト外の絶対パスおよび構築情報ファイル基準の相対パスを解決し、指定インポート先(既定: Assets/UnityTimelineBuilder/Imported)へコピーする
  - コピー先ディレクトリの自動作成とファイル名衝突時の上書き + 上書きログ出力を行う
  - 同期インポート後、返却されたアセットパスで即座にアセットをロードできる状態になっている
  - _Requirements: 2.2_
  - _Boundary: ExternalAssetImporter_
  - _Depends: 2_

- [ ] 4.2 (P) リソースリゾルバの契約とレジストリを実装する
  - リソース種別キーと解決アセット型を宣言し、行のリソース解決を試行するリゾルバ契約を定義する
  - 種別キーからリゾルバを登録・検索するレジストリ(組込みリゾルバの静的初期登録、Register による追加、テスト用リセット)を実装する
  - 既存コードを変更せず Register 呼び出しだけで新リソース種別を追加できることを単体テストで確認できる
  - _Requirements: 2.7, 2.8_
  - _Boundary: IResourceResolver, ResourceResolverRegistry_
  - _Depends: 2_

- [ ] 4.3 AudioClip リゾルバを実装する
  - Assets/ 始まりのパスはコピーせず既存アセットとして参照し、それ以外は外部インポート経由で解決する
  - 拡張子 wav / mp3 を AudioClip として解決し、解決結果の型検査を行う
  - ファイル・アセット不在および種別不一致を、行番号・参照パス付きのエラーとして報告する
  - Assets/ 参照・外部パス・不在・型不一致の各ケースで期待どおりの解決結果またはエラーが返ることを確認できる
  - _Requirements: 2.1, 2.3, 2.6, 2.7_

- [ ] 4.4 (P) AnimationClip リゾルバを実装する
  - Assets/ 始まりのパスは既存参照、それ以外は外部インポート経由で解決する(4.3 と同じ共通アルゴリズム)
  - .anim 単体アセットの直接ロードと、fbx サブアセット列挙(`__preview__` 除外)による AnimationClip 解決を実装する
  - fbx に複数クリップが内包される場合はクリップ名との名前一致で選択し、不一致時は候補名一覧を含む行番号付きエラーを報告する(legacy クリップは警告ログのみで継続)
  - .anim / 単一内包 fbx / 複数内包 fbx の名前一致・不一致、不在・型不一致の各ケースで期待どおりの結果が返ることを確認できる
  - 4.3 とはファイルが独立しているため並行可能
  - _Requirements: 2.1, 2.4, 2.5, 2.6, 2.7_
  - _Boundary: AnimationClipResolver_
  - _Depends: 4.1, 4.2_

- [ ] 5. Building: Timeline と Prefab の生成を実装する
- [ ] 5.1 (P) トラックビルダーの契約とレジストリを実装する
  - トラック種別キー・要求リソース種別の宣言、トラック作成、クリップ追加を行うビルダー契約を定義する
  - 種別キー(大文字小文字無視)からビルダーを登録・検索するレジストリ(組込みの静的初期登録、Register による追加、既知種別判定、テスト用リセット)を実装する
  - 既存コードを変更せず Register 呼び出しだけで新トラック種別を追加できることを単体テストで確認できる
  - _Requirements: 3.2, 3.6_
  - _Boundary: ITrackBuilder, TrackBuilderRegistry_
  - _Depends: 2_

- [ ] 5.2 AudioTrack ビルダーを実装する
  - TimelineAsset に指定名の AudioTrack を作成し、AudioClip を割り当てたクリップを追加する
  - クリップの start / clipIn / duration / displayName に構築情報の値を設定する
  - クリップ追加 1 回につきクリップが厳密に 1 つ追加され、トラックバインディングが未設定のままであることを確認できる
  - _Requirements: 3.2, 3.4_

- [ ] 5.3 (P) AnimationTrack ビルダーを実装する
  - TimelineAsset に指定名の AnimationTrack を作成し、AnimationClip を割り当てたクリップを追加する
  - クリップの start / clipIn / duration / displayName に構築情報の値を設定する
  - クリップ追加 1 回につきクリップが厳密に 1 つ追加され、トラックバインディングが未設定のままであることを確認できる
  - 5.2 とはファイルが独立しているため並行可能
  - _Requirements: 3.2, 3.4_
  - _Boundary: AnimationTrackBuilder_
  - _Depends: 5.1_

- [ ] 5.4 TimelineAsset ファクトリ(グルーピング・保存・上書き)を実装する
  - 行を(トラック種別の正規化小文字, トラック名完全一致)でグルーピングし、同一キーの行を同一トラックへ行出現順に集約する
  - 行に対応しないトラック・クリップを一切生成せず、クリップ総数 = 行数を保証する
  - 新規時はアセット作成、既存時はロードして全トラック削除後にインプレース再構築し(GUID 保持)、上書きした旨をログ出力して保存する
  - 指定パスに TimelineAsset が永続化され、トラック数 = グルーピングキー数・クリップ総数 = 行数となることを確認できる
  - _Requirements: 3.1, 3.3, 3.5, 4.4_

- [ ] 5.5 (P) Prefab ファクトリを実装する
  - PlayableDirector を持つ GameObject を生成し、playableAsset に TimelineAsset を設定して指定パスへ Prefab として保存する
  - トラックバインディング設定 API を一切呼ばず、既存 Prefab は上書きして上書きログを出力する
  - 保存後の一時 GameObject を finally 保証で必ず破棄し、ヒエラルキーに残留物がない状態で Prefab アセットが存在することを確認できる
  - 5.1〜5.4 の成果物には依存しない(TimelineAsset は引数で受け取る)ため並行可能
  - _Requirements: 4.1, 4.2, 4.3, 4.4_
  - _Boundary: PrefabFactory_
  - _Depends: 2_

- [ ] 6. 公開 API ファサードでパイプラインを統合する
  - 引数検証(null / 空文字 / シート不在 / 出力先・インポート先が Assets/ 配下でない)を行い、不正引数を例外またはエラー結果で通知する
  - Phase A(パース → 全行検証 → 全リソース解決。エラーは中断せず全件収集)と Phase B(Timeline 生成 → Prefab 生成。Phase A エラー 0 件のみ実行)のゲート制御を実装する
  - パーサーへのトラック種別判定関数の注入、予期しない例外のエラー変換、`[UnityTimelineBuilder]` 接頭辞付きログを実装する
  - public static メソッド 1 回の呼び出しで CSV/TSV から TimelineAsset と Prefab が生成され、成否・出力パス・全エラーを含む結果が返却されることを確認できる
  - 引数検証の単体テスト(null / 空文字 / Assets/ 外の出力先)がすべてパスする
  - _Requirements: 5.1, 5.2, 5.3_

- [ ] 7. CLI エントリポイント(バッチ実行)を実装する
  - コマンドライン引数から必須の構築情報パス・出力先と、任意のアセット名・インポート先を読み取り構築要求を組み立てる
  - 処理本体を exit code を返す純粋関数として分離し、-executeMethod 用エントリポイントはその結果を Editor 終了処理へ渡すだけにする
  - 成功時は exit code 0 と生成アセット 2 件のパスをログ出力し、構築失敗は exit code 1 + 全エラーの逐次ログ、引数不正は exit code 2 に写像する(全例外を捕捉)
  - 引数解析(必須欠落 → 2、既定値補完)と結果 → exit code 写像の単体テストがすべてパスする
  - _Requirements: 6.1, 6.2, 6.3, 6.4, 6.5, 6.6_

- [ ] 8. (P) CSV テンプレートと列定義ドキュメントを同梱する
  - 設計の列仕様表と一致するヘッダー行と、Audio / Animation 各 1 行以上のサンプルデータを含むテンプレート CSV(UTF-8・CRLF)を Documentation~ に配置する
  - 各列の名称・意味・データ型・必須/任意・記入例と、トラック種別ごとのリソースパス規約を記載した列定義ドキュメントを Documentation~ に配置する
  - テンプレート CSV が Google スプレッドシートの File > Import で崩れずに取り込めることを確認できる
  - コード成果物に依存しないためタスク 2〜7 と並行可能
  - _Requirements: 7.1, 7.2, 7.3, 7.4_
  - _Boundary: Package Artifacts_
  - _Depends: 1.1_

- [ ] 9. Validation: 統合テストと E2E で全体を検証する
- [ ] 9.1 Timeline 構築の統合テストを作成する
  - Audio / Animation 混在のフィクスチャ CSV とテスト用アセットを Tests 配下に用意する
  - 構築実行後の TimelineAsset について、トラック数・トラック種別・トラック名、同一トラックへのクリップ集約、各クリップの start / clipIn / duration / displayName と割り当てアセットを検証する
  - 記載行に対応しないトラック・クリップが生成されていないことを含め、統合テストがすべてパスする
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [ ] 9.2 (P) 外部リソースインポートの統合テストを作成する
  - プロジェクト外に配置した wav / mp3 / fbx フィクスチャ(複数 AnimationClip 内包 fbx を含む)を用意する
  - 外部パス指定でのインポート先へのコピー・参照解決、fbx 複数内包時の名前一致選択と不一致エラーを検証する
  - 外部リソース経由の構築が成功し、不一致ケースが行番号付きエラーで失敗することをテストで確認できる
  - 9.1 とはテストファイル・フィクスチャが独立しているため並行可能
  - _Requirements: 2.2, 2.3, 2.4, 2.5_
  - _Boundary: Tests(Resources 系)_
  - _Depends: 6_

- [ ] 9.3 (P) Prefab 生成と上書き冪等性の統合テストを作成する
  - 生成 Prefab の PlayableDirector 存在・playableAsset 参照・トラックバインディング未設定・一時 GameObject の残留なしを検証する
  - 同一出力先への 2 回実行で、上書きログの出力・最終状態の正しさ・TimelineAsset と Prefab のアセット GUID が 1 回目から不変であることを検証する
  - 上記をすべて検証する統合テストがパスする
  - _Requirements: 4.1, 4.2, 4.3, 4.4_
  - _Boundary: Tests(Prefab / 冪等性系)_
  - _Depends: 6_

- [ ] 9.4 (P) 同梱テンプレートを入力とする E2E テストを作成する
  - Documentation~ のテンプレート CSV をそのまま入力として構築を実行し、成功することを検証する(テンプレート・ドキュメント・パーサー仕様の一致を自動担保)
  - テンプレート内サンプル行が参照するテスト用リソースを解決可能な形で用意する
  - E2E テストがパスし、テンプレートとパーサー仕様の不一致がテスト失敗として検出される状態になっている
  - _Requirements: 7.2, 7.4_
  - _Boundary: Tests(E2E)_
  - _Depends: 6, 8_

- [ ] 9.5 CLI バッチ実行の受け入れ検証を行う
  - -batchmode / -executeMethod で実際に Unity Editor を起動する検証スクリプトを用意し、成功(exit code 0)・構築失敗(1)・引数不正(2)の 3 系統を確認する
  - 成功時ログに生成アセットパス 2 件、失敗時ログに原因特定可能なエラーメッセージが `[UnityTimelineBuilder]` 接頭辞付きで出力されることを確認する
  - 同一プロジェクトを排他使用するため 9.1〜9.4 の EditMode テスト実行とは並行不可(順次実行)
  - 3 系統すべての exit code とログが期待どおりであることをスクリプト実行結果で確認できる
  - _Requirements: 6.1, 6.3, 6.4, 6.5, 6.6_
  - _Depends: 7_
