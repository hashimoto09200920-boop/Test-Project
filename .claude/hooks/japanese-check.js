// Stop フック：Claude の最後の返答が日本語で書かれているかを調べる（2026/10/25・ユーザー指示）。
// 英語ばかりの返答なら decision:"block" を返し、日本語で書き直させる。
// コードブロック・インラインコード・URL・ファイルパスは数えない。
const fs = require("fs");

let input = "";
process.stdin.on("data", (c) => (input += c));
process.stdin.on("end", () => {
  let data = {};
  try { data = JSON.parse(input || "{}"); } catch { return; }
  const path = data.transcript_path;
  if (!path || !fs.existsSync(path)) return;

  // 最後の「ユーザーの発言」より後の、Claude の返答の文章を集める
  // 会話の記録は非常に大きくなるため、最後の2MBだけを読む（最後の返答はその中にある）
  const size = fs.statSync(path).size;
  const len = Math.min(size, 2 * 1024 * 1024);
  const buf = Buffer.alloc(len);
  const fd = fs.openSync(path, "r");
  fs.readSync(fd, buf, 0, len, size - len);
  fs.closeSync(fd);
  const lines = buf.toString("utf8").split("\n").filter(Boolean);
  if (len < size) lines.shift(); // 途中から読んだ最初の1行は欠けているので捨てる
  const texts = [];
  for (let i = lines.length - 1; i >= 0; i--) {
    let e;
    try { e = JSON.parse(lines[i]); } catch { continue; }
    const content = e.message && e.message.content;
    if (e.type === "user") {
      const isToolResult = Array.isArray(content) && content.some((b) => b && b.type === "tool_result");
      if (!isToolResult) break; // ユーザーの発言に着いたら終わり
      continue;
    }
    if (e.type === "assistant" && Array.isArray(content)) {
      for (const b of content) if (b && b.type === "text" && b.text) texts.unshift(b.text);
    }
  }
  // 作業の途中に書いた短い文章もユーザーに見えるので、この返答の中の文章を1つずつ全部調べる
  const bad = texts.find((t) => isMostlyEnglish(t));
  if (!bad) return;

  // 書き直し後もまだ英語なら、無限に止め続けないよう警告だけ出して終える
  if (data.stop_hook_active) {
    process.stdout.write(JSON.stringify({ systemMessage: "日本語チェック：書き直し後も英語の割合が多い返答でした。" }));
    return;
  }
  process.stdout.write(JSON.stringify({
    decision: "block",
    reason: "この返答の中に英語で書いた文章があります（「" + bad.slice(0, 60).replace(/\s+/g, " ") + "」）。ユーザーへの文章は、作業の途中の短い報告も含めて必ず日本語（です・ます調）で書く決まりです。その文章を日本語で書き直して、ユーザーに伝えてください。",
  }));
});

function isMostlyEnglish(text) {
  const prose = text
    .replace(/```[\s\S]*?```/g, " ")
    .replace(/`[^`]*`/g, " ")
    .replace(/\[([^\]]*)\]\([^)]*\)/g, "$1")
    .replace(/https?:\/\/\S+/g, " ")
    .replace(/[A-Za-z]:[\\/]\S+|\S+\.(cs|js|json|asset|unity|prefab|png|mp3|md|txt|log|apk|aab)\b/g, " ");

  const jp = (prose.match(/[぀-ヿ㐀-鿿ｦ-ﾟ]/g) || []).length;
  const en = (prose.match(/[A-Za-z]/g) || []).length;
  if (jp + en < 15) return false; // 短すぎる文章は判定しない
  const ratio = jp / (jp + en * 0.35); // 英単語は1文字あたりの情報量が少ないので重みを下げる
  return ratio < 0.5;
}
