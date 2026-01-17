
export interface LanguageDefinition {
  name: string;
  code: string;
  langCode: string;
  displayName: string;
  flag: string;
}

export const ALL_LANGUAGES: LanguageDefinition[] = [
  { name: "English (US)", code: "en-US, en-GB, en", langCode: "en", displayName: "English", flag: "🇺🇸" },
  { name: "Spanish (Spain)", code: "es-ES, es, es-MX, es-US", langCode: "es", displayName: "Spanish", flag: "🇪🇸" },
  { name: "French (France)", code: "fr-FR, fr", langCode: "fr", displayName: "French", flag: "🇫🇷" },
  { name: "German (Germany)", code: "de-DE, de", langCode: "de", displayName: "German", flag: "🇩🇪" },
  { name: "Italian (Italy)", code: "it-IT, it", langCode: "it", displayName: "Italian", flag: "🇮🇹" },
  { name: "Japanese (Japan)", code: "ja-JP, ja", langCode: "ja", displayName: "Japanese", flag: "🇯🇵" },
  { name: "Finnish (Finland)", code: "fi-FI, fi", langCode: "fi", displayName: "Finnish", flag: "🇫🇮" },
  { name: "Korean (South Korea)", code: "ko-KR, ko", langCode: "ko", displayName: "Korean", flag: "🇰🇷" },
  { name: "Portuguese (Brazil)", code: "pt-BR, pt, pt-PT", langCode: "pt", displayName: "Portuguese", flag: "🇧🇷" },
  { name: "Chinese (Mandarin)", code: "zh-CN, zh", langCode: "zh", displayName: "Chinese", flag: "🇨🇳" },
  { name: "Russian (Russia)", code: "ru-RU, ru", langCode: "ru", displayName: "Russian", flag: "🇷🇺" },
  { name: "Swedish (Sweden)", code: "sv-SE, sv", langCode: "sv", displayName: "Swedish", flag: "🇸🇪" }
];

export const DEFAULT_NATIVE_LANG_CODE = "en";
export const DEFAULT_TARGET_LANG_CODE = "es";

export const STT_LANGUAGES = ALL_LANGUAGES.map(l => ({ name: l.displayName, code: l.code.split(',')[0] }));
