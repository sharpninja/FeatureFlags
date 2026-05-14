# McpServer Debugging Prompt

You are debugging the local McpServer / mcpserver-codex-plugin integration on Windows.

Workspace:
- Repo: `F:\GitHub\FeatureFlags`
- Plugin repo: `F:\GitHub\mcpserver-codex-plugin`
- Git Bash: `C:\Program Files\Git\bin\bash.exe`
- Session id: `Codex-20260514T035353Z-featureflags-phase-0-bootstrap`
- Marker: `F:\GitHub\FeatureFlags\AGENTS-README-FIRST.yaml`

Required constraints:
- Use `mcpserver-codex-plugin`; do not bypass it except to isolate a plugin/server defect.
- Validate marker trust through `agents-readme-first.yaml` / `marker-resolver.sh` before MCP calls.
- Do not suppress warnings or hide failures.
- Do not commit marker/API-key files.

Known-good checks:
```powershell
$gitBash = 'C:\Program Files\Git\bin\bash.exe'
& $gitBash -lc 'cd /f/GitHub/FeatureFlags; export CODEX_PLUGIN_ROOT=/f/GitHub/mcpserver-codex-plugin; export PLUGIN_ROOT_OVERRIDE=/f/GitHub/mcpserver-codex-plugin; export PLUGIN_AGENT_NAME=Codex; source /f/GitHub/mcpserver-codex-plugin/lib/marker-resolver.sh; full_bootstrap /f/GitHub/FeatureFlags && echo "marker=trusted workspace=$MCPSERVER_WORKSPACE baseUrl=$MCPSERVER_BASE_URL"'
```

Expected trust result:
```text
marker=trusted workspace=FeatureFlags baseUrl=http://PAYTON-LEGION2:7147
```

Session-log query works:
```powershell
$gitBash = 'C:\Program Files\Git\bin\bash.exe'
& $gitBash -lc 'cd /f/GitHub/FeatureFlags; export CODEX_PLUGIN_ROOT=/f/GitHub/mcpserver-codex-plugin; export PLUGIN_ROOT_OVERRIDE=/f/GitHub/mcpserver-codex-plugin; export PLUGIN_AGENT_NAME=Codex; source /f/GitHub/mcpserver-codex-plugin/lib/repl-invoke.sh; repl_invoke "workflow.sessionlog.queryHistory" "limit: 3"'
```

Observed issues to debug:

1. A combined plugin logging command timed out after 124 seconds while running `workflow.sessionlog.appendActions`, `workflow.todo.create`, and `workflow.sessionlog.completeTurn` in one Git Bash process. Afterward, `workflow.sessionlog.queryHistory` showed `completeTurn` had persisted, so the timeout likely occurred around action/TODO logging or process cleanup.

2. `workflow.todo.query` works and returns an empty set for the new TODO id:
```yaml
type: result
payload:
  result: |
    {"items":[],"totalCount":0}
```

3. `workflow.todo.create` without a `request:` wrapper fails against the typed REPL path:
```text
code: method_invocation_error
message: 'Missing required parameter: request (type: TodoCreateRequest)'
details:
  clientName: Todo
  methodName: CreateAsync
  exceptionType: System.ArgumentException
```

4. `client.Todo.CreateAsync` with a `request:` wrapper fails authentication:
```text
message: 'Authentication required: no credential is configured on this client. Set BearerToken (for interactive users via OIDC) or ApiKey (for agents via the AGENTS-README-FIRST.yaml marker file) before calling any endpoint.'
details:
  clientName: Todo
  methodName: CreateAsync
  exceptionType: System.InvalidOperationException
```

5. `workflow.todo.create` with a `request:` wrapper still fails authentication instead of succeeding through marker-backed compat auth or HTTP fallback.

6. Direct `_repl_todo_http_fallback "create" ...` exits `1` with no useful output, so the HTTP fallback path needs better error reporting and likely body/route/auth validation for create.

Important plugin context:
- `workflow.sessionlog.*` required a local fix in `F:\GitHub\mcpserver-codex-plugin\lib\repl-invoke.sh` because YAML numeric scalars were serialized as strings; `turnCount`, `totalTokens`, and `tokenCount` needed `!!int`.
- `_repl_workflow_append_dialog` also needed to call `_repl_invoke_raw_in_workspace ... "compat"` first so marker/auth context is available.
- Focused regression passed:
```powershell
$gitBash = 'C:\Program Files\Git\bin\bash.exe'
& $gitBash -lc 'cd /f/GitHub/mcpserver-codex-plugin && bats --tap -f "workflow.sessionlog" tests/repl-invoke-shim.bats'
```

Debugging goals:
- Determine whether TODO create failure is caused by plugin YAML shaping, REPL typed-client auth injection, HTTP fallback route/body/auth behavior, or server-side TODO endpoint behavior.
- Add a focused regression in `tests/repl-invoke-shim.bats` for `workflow.todo.create` with and without `request:` wrapper.
- Make `workflow.todo.create` accept the same user-facing shape as query/update, or intentionally normalize to the typed `request:` shape before invoking `client.Todo.CreateAsync`.
- Ensure HTTP fallback for TODO create emits enough diagnostic detail when it fails.
- Validate the final fix with a real live create/query/delete cycle against `F:\GitHub\FeatureFlags`, then run the focused Bats suite.

Do not continue by hand-editing `docs/todo.yaml`; the point is to fix or classify the MCP/plugin path.
