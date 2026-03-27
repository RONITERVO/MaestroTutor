// Copyright 2025 Roni Tervo
//
// SPDX-License-Identifier: Apache-2.0
export interface LanguageDefinition {
  name: string;
  code: string;
  langCode: string;
  displayName: string;
  flag: string;
  /** Short 2-3 letter code for compact display (shown with flag when languages share the same flag) */
  shortCode: string;
  /** True for languages that use non-Latin scripts and benefit from romanization (pronunciation in Latin letters) */
  needsRomanization?: boolean;
}

export const ALL_LANGUAGES: LanguageDefinition[] = [
  // English variants
  { name: "English (US)", code: "en-US", langCode: "en-US", displayName: "English (US)", flag: "🇺🇸", shortCode: "EN" },
  { name: "English (UK)", code: "en-GB", langCode: "en-GB", displayName: "English (UK)", flag: "🇬🇧", shortCode: "EN" },
  { name: "English (Australia)", code: "en-AU", langCode: "en-AU", displayName: "English (AU)", flag: "🇦🇺", shortCode: "EN" },
  { name: "English (India)", code: "en-IN", langCode: "en-IN", displayName: "English (IN)", flag: "🇮🇳", shortCode: "EN" },
  
  // Spanish variants
  { name: "Spanish (Spain)", code: "es-ES", langCode: "es-ES", displayName: "Spanish (ES)", flag: "🇪🇸", shortCode: "ES" },
  { name: "Spanish (US)", code: "es-US", langCode: "es-US", displayName: "Spanish (US)", flag: "🇲🇽", shortCode: "ES" },
  
  // French variants
  { name: "French (France)", code: "fr-FR", langCode: "fr-FR", displayName: "French (FR)", flag: "🇫🇷", shortCode: "FR" },
  { name: "French (Canada)", code: "fr-CA", langCode: "fr-CA", displayName: "French (CA)", flag: "🇨🇦", shortCode: "FR" },
  
  // German
  { name: "German (Germany)", code: "de-DE", langCode: "de-DE", displayName: "German", flag: "🇩🇪", shortCode: "DE" },
  
  // Italian
  { name: "Italian (Italy)", code: "it-IT", langCode: "it-IT", displayName: "Italian", flag: "🇮🇹", shortCode: "IT" },
  
  // Portuguese
  { name: "Portuguese (Brazil)", code: "pt-BR", langCode: "pt-BR", displayName: "Portuguese (BR)", flag: "🇧🇷", shortCode: "PT" },
  
  // Dutch
  { name: "Dutch (Netherlands)", code: "nl-NL", langCode: "nl-NL", displayName: "Dutch", flag: "🇳🇱", shortCode: "NL" },
  
  // Polish
  { name: "Polish (Poland)", code: "pl-PL", langCode: "pl-PL", displayName: "Polish", flag: "🇵🇱", shortCode: "PL" },
  
  // Russian
  { name: "Russian (Russia)", code: "ru-RU", langCode: "ru-RU", displayName: "Russian", flag: "🇷🇺", shortCode: "RU", needsRomanization: true },
  
  // Turkish
  { name: "Turkish (Turkey)", code: "tr-TR", langCode: "tr-TR", displayName: "Turkish", flag: "🇹🇷", shortCode: "TR" },
  
  // Arabic
  { name: "Arabic (Generic)", code: "ar-XA", langCode: "ar-XA", displayName: "Arabic", flag: "🇸🇦", shortCode: "AR", needsRomanization: true },
  
  // Indian languages (share 🇮🇳 flag - shortCode differentiates them)
  { name: "Hindi (India)", code: "hi-IN", langCode: "hi-IN", displayName: "Hindi", flag: "🇮🇳", shortCode: "HI", needsRomanization: true },
  { name: "Bengali (India)", code: "bn-IN", langCode: "bn-IN", displayName: "Bengali", flag: "🇮🇳", shortCode: "BN", needsRomanization: true },
  { name: "Gujarati (India)", code: "gu-IN", langCode: "gu-IN", displayName: "Gujarati", flag: "🇮🇳", shortCode: "GU", needsRomanization: true },
  { name: "Kannada (India)", code: "kn-IN", langCode: "kn-IN", displayName: "Kannada", flag: "🇮🇳", shortCode: "KN", needsRomanization: true },
  { name: "Malayalam (India)", code: "ml-IN", langCode: "ml-IN", displayName: "Malayalam", flag: "🇮🇳", shortCode: "ML", needsRomanization: true },
  { name: "Marathi (India)", code: "mr-IN", langCode: "mr-IN", displayName: "Marathi", flag: "🇮🇳", shortCode: "MR", needsRomanization: true },
  { name: "Tamil (India)", code: "ta-IN", langCode: "ta-IN", displayName: "Tamil", flag: "🇮🇳", shortCode: "TA", needsRomanization: true },
  { name: "Telugu (India)", code: "te-IN", langCode: "te-IN", displayName: "Telugu", flag: "🇮🇳", shortCode: "TE", needsRomanization: true },
  
  // East Asian
  { name: "Japanese (Japan)", code: "ja-JP", langCode: "ja-JP", displayName: "Japanese", flag: "🇯🇵", shortCode: "JA", needsRomanization: true },
  { name: "Korean (South Korea)", code: "ko-KR", langCode: "ko-KR", displayName: "Korean", flag: "🇰🇷", shortCode: "KO", needsRomanization: true },
  { name: "Mandarin Chinese (China)", code: "cmn-CN", langCode: "cmn-CN", displayName: "Chinese (Mandarin)", flag: "🇨🇳", shortCode: "ZH", needsRomanization: true },
  
  // Southeast Asian
  { name: "Thai (Thailand)", code: "th-TH", langCode: "th-TH", displayName: "Thai", flag: "🇹🇭", shortCode: "TH", needsRomanization: true },
  { name: "Vietnamese (Vietnam)", code: "vi-VN", langCode: "vi-VN", displayName: "Vietnamese", flag: "🇻🇳", shortCode: "VI" },
  { name: "Indonesian (Indonesia)", code: "id-ID", langCode: "id-ID", displayName: "Indonesian", flag: "🇮🇩", shortCode: "ID" },
  
  // Nordic
  { name: "Finnish (Finland)", code: "fi-FI", langCode: "fi-FI", displayName: "Finnish", flag: "🇫🇮", shortCode: "FI" },
  { name: "Swedish (Sweden)", code: "sv-SE", langCode: "sv-SE", displayName: "Swedish", flag: "🇸🇪", shortCode: "SV" },
];

// Helper: Check if a flag is shared by multiple languages
const flagCounts = ALL_LANGUAGES.reduce((acc, lang) => {
  acc[lang.flag] = (acc[lang.flag] || 0) + 1;
  return acc;
}, {} as Record<string, number>);

/** Returns true if this language's flag is shared with other languages */
export const hasSharedFlag = (lang: LanguageDefinition): boolean => flagCounts[lang.flag] > 1;

/** Compact badge: flag + shortCode for shared flags, just flag otherwise */
export const getCompactBadge = (lang: LanguageDefinition): string => 
  hasSharedFlag(lang) ? `${lang.flag}${lang.shortCode}` : lang.flag;

export const DEFAULT_NATIVE_LANG_CODE = "en-US";
export const DEFAULT_TARGET_LANG_CODE = "es-ES";

export const STT_LANGUAGES = ALL_LANGUAGES.map(l => ({ name: l.displayName, code: l.code.split(',')[0] }));
