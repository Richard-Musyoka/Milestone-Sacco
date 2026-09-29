import re, sys
src, dst, top_path, helpers_path, kpi_path = sys.argv[1:6]
s = open(src, encoding='utf-8-sig').read()
top = open(top_path).read(); helpers = open(helpers_path).read(); kpi = open(kpi_path).read()

# 1. replace everything before @code
i = s.index('@code {')
body = s[i:]

# 2. Money line -> helpers (constants, Pick, Preset, Money)
body, n = re.subn(r'    private static string Money\(decimal v\) => v\.ToString\("C", CultureInfo\.CurrentCulture\);\n', lambda m: helpers, body)
assert n == 1, 'Money not found'

# 3. replace Kpi + ChartCard helpers
a = body.index('    private RenderFragment Kpi(')
b = body.index('    /// <summary>SVG &lt;text&gt; built in code')
body = body[:a] + kpi + body[b:]

# 4. Rank/empty restyle
body = body.replace('<span class="text-muted fw-normal">· @Pct(p.Value, total)</span>', '<span style="color:var(--ms-text-3);font-weight:500">· @Pct(p.Value, total)</span>')
body = body.replace('<div class="text-end small text-muted mt-3">Total: <strong>', '<div class="ms-hint" style="text-align:right;margin-top:14px">Total: <strong>')
body = body.replace('fill="#fff" stroke="@color" stroke-width="2"', 'fill="#fff" stroke="@color" stroke-width="2.5"')
open(dst, 'w', encoding='utf-8-sig').write(top + body)
print('ok')
