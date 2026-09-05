import type {PrismLib} from 'prism-react-renderer';
import includeOriginalLanguages from '@theme-original/prism-include-languages';

// Presentation grammar for Terrabuild's configuration language. This highlights
// examples; the Terrabuild parser remains the authority on valid configuration.
export default function includeLanguages(
  Prism: PrismLib,
): void {
  includeOriginalLanguages(Prism);
  Prism.languages.terrabuild = {
    comment: /#.*/,
    string: {pattern: /"(?:\\.|[^"\\])*"/, greedy: true},
    builtin: /@[\w-]+/,
    constant: /~[\w-]+/,
    variable: /\b(?:terrabuild|project|target|phase|var|local)\.[\w.^-]+/,
    keyword: /\b(?:workspace|project|target|phase|extension|variable|locals|defaults|env)\b/,
    boolean: /\b(?:true|false|nothing)\b/,
    number: /\b\d+(?:\.\d+)?\b/,
    operator: /[=!?+*/<>-]+/,
    punctuation: /[{}[\]():,.]/,
  };
}
