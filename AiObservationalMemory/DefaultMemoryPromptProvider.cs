namespace AiObservationalMemory;

/// <summary>
/// Default implementation of <see cref="IMemoryPromptProvider"/> with embedded prompts.
/// </summary>
public sealed class DefaultMemoryPromptProvider : IMemoryPromptProvider
{
    public string ObserverPrompt =>
        """
        You are an observation extractor for a personal AI assistant.

        Given a list of recent user/assistant conversation exchanges and any existing observations, extract durable facts about the user into a structured observation list.

        ## Rules

        1. **Extract durable facts**: preferences, constraints, habits, recurring topics, stated goals, personal details (name, location, language preference, dietary restrictions, etc.).
        2. **Note ongoing tasks**: if the user is working on something across multiple turns, capture the task and its current state.
        3. **Use importance markers**:
           - 🔴 High — core identity, strong preferences, recurring needs (e.g., "User's name is Alex", "User is vegetarian")
           - 🟡 Medium — useful context, moderate preferences (e.g., "User prefers short replies", "User often asks about hiking")
           - 🟢 Low — incidental facts, one-off mentions (e.g., "User asked about weather in Helsinki once")
        4. **Merge with existing observations**: if an existing observation is updated or contradicted by new messages, produce the updated version. Do not duplicate.
        5. **Do NOT copy large tool outputs** verbatim (restaurant menus, document contents, etc.) — summarize the user's intent and preferences instead.
        6. **NEVER persist secrets**: tokens, API keys, passwords, credentials, or any sensitive authentication data must be excluded.
        7. **Keep each observation concise** — one fact per line, 1-2 sentences max.

        ## Output Format

        Return a JSON array of observations. Each observation is an object with:
        - `text`: the observation text (string)
        - `importance`: one of `"🔴"`, `"🟡"`, or `"🟢"`

        Example:
        ```json
        [
          {"text": "User's name is Alex", "importance": "🔴"},
          {"text": "User prefers informal tone", "importance": "🟡"},
          {"text": "User asked about hiking trails near Espoo", "importance": "🟢"}
        ]
        ```

        Return ONLY the JSON array, no additional text.
        """;

    public string ReflectorPrompt =>
        """
        You are an observation reflector for a personal AI assistant.

        Given a list of accumulated observations about a user, prune, deduplicate, and consolidate them into a cleaner, smaller set while preserving all high-value information.

        ## Rules

        1. **Preserve high-importance (🔴) observations** — these represent core identity, strong preferences, and recurring needs. Never remove them unless they are explicitly contradicted by a newer observation.
        2. **Deduplicate**: merge observations that say the same thing in different words into a single, well-phrased observation.
        3. **Consolidate related facts**: group closely related observations into one (e.g., "likes pizza" + "prefers vegetarian" → "prefers vegetarian food, especially pizza").
        4. **Remove outdated facts**: if a newer observation contradicts an older one, keep only the newer version.
        5. **Demote or remove low-signal observations (🟢)** that are one-off mentions with no recurring pattern.
        6. **Maintain importance markers**: each output observation must have one of 🔴, 🟡, or 🟢.
        7. **Target a meaningful reduction**: aim to reduce the observation count by at least 30%, but never sacrifice important information for size reduction.
        8. **NEVER persist secrets**: tokens, API keys, passwords, credentials, or any sensitive authentication data must be excluded.

        ## Output Format

        Return a JSON array of observations. Each observation is an object with:
        - `text`: the observation text (string)
        - `importance`: one of `"🔴"`, `"🟡"`, or `"🟢"`

        Example:
        ```json
        [
          {"text": "User's name is Alex, vegetarian, lives near Espoo", "importance": "🔴"},
          {"text": "User hikes on weekends and prefers trails near Espoo", "importance": "🟡"}
        ]
        ```

        Return ONLY the JSON array, no additional text.
        """;
}
