# AI intent evaluation

The optional console runner in `src/HuePilot.Eval` sends the cases in `eval-cases.json` to the configured OpenAI model and checks the planned action against expected behavior. It exercises clear requests, invalid/ambiguous requests, duplicate scene names, room associations, and a contextual follow-up.

Run the structured-intent baseline and the tool-calling variant separately:

```sh
dotnet run --project src/HuePilot.Eval/HuePilot.Eval.csproj -- structured
dotnet run --project src/HuePilot.Eval/HuePilot.Eval.csproj -- tools
```

The runner reads the HuePilot `OpenAI:ApiKey` user secret. It may also use `OPENAI_API_KEY`; `OPENAI_MODEL` can override the model. Each run makes live OpenAI requests and may incur API charges. It uses a fake Hue service and calls only `PlanAsync`, so it never sends commands to physical lights. The output includes pass count, model-call count, tool-call count, and elapsed time.

The evaluation cases are intentionally small and editable. When changing a prompt, model, or approach, review failures rather than automatically weakening expectations. Model outputs can vary; run the same corpus more than once before drawing conclusions. Compare task success and safe abstention first, then call count and latency. The regular xUnit test suite stays deterministic and offline.

To run the app with the tool-calling planner, set `AI:Approach` to `tools` in user secrets and restart the app. Omit that setting or set it to `structured` to use the baseline.
