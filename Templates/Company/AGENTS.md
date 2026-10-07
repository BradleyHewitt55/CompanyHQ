# Company project

- The company owns its apps, assets, configuration, and scripts; generic service changes belong upstream.
- Keep lifecycle automation minimal. Operational details belong in executable files; script-management rules belong in Scripts/AGENTS.md.
- Preserve persistent data and credentials. Do not commit environment secrets, generated local state, or test output.
- Run only the minimum checks relevant to a change, using isolated backing services.
- Keep generic dependencies in their isolated Git checkout and record the selected commit; do not edit managed dependency code directly.
