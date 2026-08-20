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

## Requirements
<!-- Will be generated in /kiro-spec-requirements phase -->
