namespace Hidano.UnityTimelineBuilder.Editor
{
    /// <summary>Timeline と Prefab の構築に必要な入力パラメーター。</summary>
    public sealed class BuildRequest
    {
        /// <summary>構築情報 CSV/TSV のパス。</summary>
        public string SheetPath { get; set; }

        /// <summary>生成アセットの出力先。Assets/ 配下を指定する。</summary>
        public string OutputDirectory { get; set; }

        /// <summary>生成アセット名。未指定時はシートファイル名を使用する。</summary>
        public string AssetName { get; set; }

        /// <summary>外部リソースのインポート先。明示指定時はそこへ集約し、
        /// 未指定時は各グループフォルダ配下（AudioClips / Animations）へ取り込む。</summary>
        public string ImportDirectory { get; set; }
    }
}
