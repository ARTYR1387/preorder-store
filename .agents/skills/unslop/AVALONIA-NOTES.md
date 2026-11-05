# Using unslop with Avalonia

The upstream skill documents Claude Code and Codex workflows and lists React, Tailwind, HTML, and CSS among its examples. Avalonia XAML is not listed as an officially tested stack. This project therefore uses the upstream material as a human design checklist; it does not claim that unslop can inspect or transform Avalonia automatically.

Applied principles:

- Keep shell surfaces neutral; reserve one restrained green accent for primary actions and selected information.
- Use a consistent 4 px spacing rhythm and align repeated catalogue items.
- Use sentence-case Russian UI copy, real labels, and no decorative emoji or promotional filler.
- Prefer product imagery as the catalogue's rich visual element; avoid gradients, glows, and ornamental effects.
- Keep navigation compact and desktop-native, and let content reflow through a scrollable catalogue.
- Provide visible empty, error, busy, and disabled states where the implementation supports them.

Source: `SKILL.md` and `references/` in this directory, from [heydamir/unslop](https://github.com/heydamir/unslop). Author: Damir Sokolovsky. License: CC BY 4.0; preserve this credit when redistributing or adapting the skill.
