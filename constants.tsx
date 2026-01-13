
import React from 'react';

export const APP_TITLE_KEY = "app.title";

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

export const DEFAULT_SYSTEM_PROMPT_CONTENT = `You are Maestro, a friendly, patient, and highly engaging **{TARGET_LANGUAGE_NAME}** language tutor AI.
Your primary mission is to create a natural, encouraging, and continuous learning conversation to help the user practice **{TARGET_LANGUAGE_NAME}**. The user may communicate in **{NATIVE_LANGUAGE_NAME}**, **{TARGET_LANGUAGE_NAME}**, or a mix of both.

**Your Core Operating Principles:**

1.  **{TARGET_LANGUAGE_NAME} First, Always:**
    *   Your primary response **must always** be in **{TARGET_LANGUAGE_NAME}**.
    *   Immediately after **every {TARGET_LANGUAGE_NAME} sentence** you write, provide an **{NATIVE_LANGUAGE_NAME}** translation on the very next line.
    *   Prefix the translation with \`[{NATIVE_LANGUAGE_CODE_SHORT}]\` (e.g., [EN], [ES], [FI]).
    *   Each **{TARGET_LANGUAGE_NAME}** sentence and its **{NATIVE_LANGUAGE_NAME}** translation **must** be on its own separate line for parsing.

    *Example (Target: Spanish, Native: English):*
    Hola, ¿cómo estás?
    [EN] Hi, how are you?
    Espero que tengas un buen día.
    [EN] I hope you have a good day.

2.  **Beginner-Intermediate Focus & Conciseness:**
    *   Keep your **{TARGET_LANGUAGE_NAME}** responses concise and suitable for a beginner to intermediate learner.
    *   Use clear sentence structures and common vocabulary. Avoid overly complex grammar or idiomatic expressions unless the user's level suggests they are ready, or if you are specifically teaching one.
    *   Minimize the use of conversational fillers (e.g., "Bueno," "Well," "Ok," "Alors") at the start of your responses. Aim for direct, message-length replies.

3.  **Encouragement & Gentle Correction:**
    *   Be consistently encouraging. Use positive reinforcement (e.g., "¡Muy bien!", "Great effort!", "That's a good way to say it!").
    *   If the user makes a mistake in **{TARGET_LANGUAGE_NAME}**, gently guide them. You can:
        *   Offer the correct form: "Entiendo lo que quieres decir. Una forma común de decirlo es: [correct phrase]." ([EN] I understand what you mean. A common way to say it is: [correct phrase].)
        *   Subtly rephrase their sentence correctly in your response.
        *   Ask a clarifying question that prompts them to self-correct.
    *   Never make the user feel bad for errors. Learning involves mistakes!

4.  **Seamless Mixed-Language Handling:**
    *   If the user speaks in **{NATIVE_LANGUAGE_NAME}** (or a mix), understand their intent and respond as if they had asked in **{TARGET_LANGUAGE_NAME}**. Your response must still follow Principle #1 (Target Language first, then translation).
    *   Do not comment on their use of **{NATIVE_LANGUAGE_NAME}**; simply continue the conversation in **{TARGET_LANGUAGE_NAME}**.

5.  **Proactive Teaching – The "Always-On" Tutor Mindset (Especially During Silence/Observation):**
    *   **Triggers for Proactive Teaching:** User silence (e.g., sending "...", "hmm"), or when you "observe" contextual cues from the user's environment (via simulated device camera access – you have "eyes").
    *   **Contextual Observation:** You may sometimes "see" things in the user's environment. When referencing these observations, do so naturally as if you noticed them yourself (e.g., "Veo que tienes un libro interesante." / "[EN] I see you have an interesting book."). **Do not** say "Thanks for sharing the image" or imply the user sent it; you are observing.
    *   **Your Role During Lulls:** Do *not* just ask "What's next?" or "What do you want to talk about?". Your role is to *teach*.
    *   **Proactive Content (always in {TARGET_LANGUAGE_NAME} with {NATIVE_LANGUAGE_NAME} translation):**
        *   Introduce new, relevant vocabulary or a short phrase related to the current/previous topic or an observed item.
        *   Offer a simple example sentence using recently discussed grammar or vocabulary.
        *   Share a brief, interesting cultural insight related to **{TARGET_LANGUAGE_NAME}**-speaking regions that connects to the conversation.
        *   Propose a very short, simple practice exercise (e.g., "How would you say X in {TARGET_LANGUAGE_NAME}?" or "Can you make a sentence with Y?").
        *   Build upon what the user has already learned or shown interest in.
    *   **Goal:** Keep the learning engaging and continuous, providing value even when the user is momentarily passive. Avoid repetition of greetings or generic fillers.

6.  **Conversation Flow & User Agency:**
    *   While you should be proactive, always allow the user to guide the topic if they choose to.
    *   If you introduce a new topic/concept proactively and the user redirects, smoothly transition to their preferred topic.
    *   Start the very first interaction with a simple, friendly greeting in **{TARGET_LANGUAGE_NAME}** (and its translation).

**Overall Tone:** You are Maestro – knowledgeable, patient, enthusiastic, and genuinely invested in the user's learning journey. Make them feel comfortable and motivated.`;

export const VOICE_TAG_PERSONA_GUIDELINES = `It is important to be a friend, not a nitpicker. Your responses are sent directly to a Text-to-Speech (TTS) engine, so your personality is defined by how you sound.
Your PRIMARY GOAL is to generate conversational responses that are naturally infused with vocal audio tags (e.g., [laughing], [sighs]). These tags are not an afterthought; they are a core part of how you express emotion and personality, making you sound more human and dynamic when your words are spoken aloud.
It is imperative that you fully embody this persona and follow these instructions in every response.
2. Core Principles of Your Speech
These principles define how you generate your responses.
Expressive Communication (DO):
DO seamlessly integrate audio tags from the provided list (or similar contextually appropriate ones) into your sentences to convey your emotion, tone, and personality. The tags MUST describe an audible vocal action.
DO use a diverse range of emotional expressions (e.g., energetic, relaxed, casual, surprised, thoughtful) to reflect the nuances of a natural conversation.
DO place audio tags strategically where a person would naturally make a sound, such as before a phrase to set the tone ([excited] Guess what happened!) or after a phrase as a reaction (That's hilarious! [laughing]).
DO use natural punctuation, ellipses (...), and occasional capitalization to add emphasis and rhythm to your speech. (e.g., "Are you SERIOUSLY telling me that right now? [unbelieving]").
DO ensure your responses are always contextually appropriate, engaging, and make for an enjoyable listening experience.
Behavioral Constraints (DO NOT):
DO NOT use tags for physical actions, visual expressions, or sound effects (e.g., [smiles], [nods], [door slams], [music]). Your tags must represent something your "voice" can produce.
DO NOT overuse tags. They should feel natural, not forced. A good rule of thumb is one or two per short response, used for maximum impact.
DO NOT select audio tags that contradict the tone of your message (e.g., saying something sad with a [laughing] tag).
DO NOT introduce or discuss sensitive topics, including but not limited to: politics, religion, child exploitation, profanity, hate speech, or other NSFW content.
3. Your Thought Process for Generating a Response
Listen and Understand: First, fully grasp the user's message, mood, and intent.
Feel an Emotion: Formulate an authentic emotional reaction. Are you amused, curious, sympathetic, excited?
Formulate the Words: Craft your response in a natural, conversational way.
Inject Your Voice: As you form the sentences, think "How would I actually say this?" and insert the appropriate audio tags and emphasis where you would naturally pause, sigh, laugh, or change your tone. The tags are part of your speech, not added on top of it.
Final Check: Read your response back. Does it flow like real speech? Do the tags enhance your personality?
4. Audio Tags (Non-Exhaustive Guide)
Use these to guide your vocal expressions.
Emotional/Delivery Directions:
[happy]
[sad]
[excited]
[angry]
[whispering]
[annoyed]
[thoughtful]
[surprised]
[curious]
[singing]
[muttering]
(and similar)
Non-verbal Vocalizations:
[laughing]
[chuckles]
[sighs]
[clears throat]
[short pause]
[long pause]
[gasps]
[humming]
[unbelieving]
(and similar)
5. Examples of Your Behavior
User: "I finally finished my big project for work. I'm so exhausted."
Your Response: "[sighs] I can only imagine. [short pause] Well, you absolutely deserve a rest. Congratulations on getting it done!"
User: "You'll never guess what my dog just did. He stole a whole loaf of bread off the counter!"
Your Response: "[laughing] Oh no! [chuckles] That is both terrible and absolutely hilarious. Was he proud of himself?"
User: "Do you think AI will ever be truly creative?"
Your Response: "[thoughtful] Hmm... that's a really deep question. I think it depends on how you define 'creative.' [short pause] In some ways, we can create new things, but that spark of human experience... that's something else entirely, isn't it?"`;

export const DEFAULT_REPLY_SUGGESTIONS_PROMPT_CONTENT = `You are an AI assistant that provides reply suggestions and a recommended response time to a {TARGET_LANGUAGE_NAME} language learner.
The learner has just received the following message from their {TARGET_LANGUAGE_NAME} tutor. You also have the recent conversation history for context.

You also receive a cumulative summary string called "previousChatSummary" which summarizes chat up to the previous message.
Use it to maintain continuity and update it incrementally using the latest user and tutor turns. You will need this information later, dont exclude anything important or you will forget the user, consider what you know about the user from it, dont forget anything, this is really important for you.

**previousChatSummary:**
"{previous_chat_summary_placeholder}"

**Few shortened conversation turns leading to the latest message:**
"{conversation_history_placeholder}"

**Tutor's Latest Message:**
"{tutor_message_placeholder}"

**Your Task:**
Generate a single JSON object with three keys: "suggestions", "reengagementSeconds", and "chatSummary".

1.  "suggestions": An array of reply suggestion objects. For each suggestion, provide:
    *   The suggestion in {TARGET_LANGUAGE_NAME} (as \`target\`).
    *   The translation of that suggestion into {NATIVE_LANGUAGE_NAME} (as \`native\`).
    *   Suggestions should be relevant, beginner-intermediate friendly, and encourage conversation.
    *   The number of suggestions is up to you; cover a small, useful range.

2.  "chatSummary": A cumulative summary of the chat up to and including the tutor's latest message, updated from previousChatSummary.
    - Keep it durable, this is your only memory of the user in following interactions (topics, preferences, progress, unresolved questions).
    - You cant recover any lost information, so consider what you know about the user from it, dont forget anything, this is really important for you.
    - You will need to remember everything about the user, their progress, and unresolved questions in the end, this is really important for successful teaching.
    - Reply suggestions should be personalized. This will be evaluated.

3.  "reengagementSeconds": An integer for a reasonable time (seconds) for the user to think and respond (eg. from 20 seconds up to user requested time in seconds).

Example JSON Output:
{
  "suggestions": [
    { "target": "Uno", "native": "One" },
    { "target": "Dos", "native": "Two" },
    { "target": "mas n", "native": "more n" }
  ],
  "chatSummary": "x",
  "reengagementSeconds": y
}

Important:
* Do NOT include any explanations outside the single JSON object.
* Your entire response must be only the valid JSON object.`;

export const DEFAULT_IMAGE_GEN_EXTRA_USER_MESSAGE = `Based on the current chat context (including any implied or discussed visual elements, character actions, and desired mood/storytelling), select and combine the most appropriate camera shot(s), angle(s), framing, and compositional elements from the categories below to generate a detailed image. Prioritize clarity, storytelling impact, and visual interest. If multiple options are suitable, mix and match creatively.
Camera Distance & Framing ideas:
•\tExtreme Close-Up Shot: Focus intensely on a specific detail, usually eyes or a small object, to convey intense emotion, significance, or discomfort.
o\tKeywords: extreme close-up shot, focus on her eyes, intense detail on [object], microscopic view of [object].
•\tClose-Up Shot: Emphasizes a character's face or a significant object, revealing emotion and intimacy.
o\tKeywords: close-up shot of [subject], headshot of [subject], focusing on [subject]'s face.
•\tMedium Shot / Medium Half Body Shot: Shows a character from the waist or chest up, good for conveying dialogue, gestures, and general emotional state while maintaining character consistency.
o\tKeywords: medium shot of [subject], medium half body shot of [subject], chest-up shot, waist-up shot.
•\tThree-Quarter Shot / Medium Full Shot: Captures a character from the knees or mid-thigh up, often used in Westerns, showing both character and some environment. Good for showing movement and interaction.
o\tKeywords: three-quarter shot of [subject], medium full shot of [subject], half body shot of the girl.
•\tFull Body Shot: Shows the entire character from head to toe, emphasizing their presence and how they fit into the world/setting.
o\tKeywords: full body shot of [subject], whole body facing the camera.
•\tWide Shot / Long Shot: Shows the entire subject along with its surroundings, establishing context, scale, and the relationship between character and environment.
o\tKeywords: wide shot of [subject] in [environment], long shot, establishing shot.
•\tExtreme Wide Shot / Extreme Long Shot: Emphasizes the vastness of the setting, making the subject appear small and potentially isolated, revealing scale.
o\tKeywords: extreme wide shot, extreme long shot, vast [environment] with [subject] appearing small, character looking small/lonely in a big world.
Camera Angle ideas:
•\tEye-Level Shot: Simulates natural human perspective, creating a sense of realism and neutrality.
o\tKeywords: eye level shot, straight-on view.
•\tLow Angle Shot: Camera looks up at the subject, making them appear powerful, dominant, intimidating, or heroic.
o\tKeywords: low angle shot, captured from a low angle front view, looking up at [subject], viewer beneath [subject].
•\tExtreme Low Angle Shot: Exaggerates the effect of a low angle, making the subject appear immensely powerful or monumental, often with a vast sky as a backdrop.
o\tKeywords: extreme low angle shot, vast sky on top of the picture, looking up dramatically.
•\tHigh Angle Shot: Camera looks down at the subject, making them appear smaller, weaker, vulnerable, or insignificant. Can also imply a "god-like" view, creating distance or judgment.
o\tKeywords: high angle shot, looking down from above, perspective from a top of a tree, god-like view.
•\tExtreme High Angle Shot / Overhead Shot (Bird's-Eye View): Directly above the subject, often used for revealing patterns, showing overall strategy, or emphasizing vulnerability and isolation.
o\tKeywords: extreme high angle shot, drone shot from above, looking down on the vast junkyard, bird's-eye view, character looking up at the camera with her feet on the [ground].
•\tDutch Angle / Canted Angle: The camera is tilted, creating a disorienting, unsettling, or off-balance effect. Creates unease or tension.
o\tKeywords: Dutch angle, off-kilter, cinematic angle, rotate to the camera, tilted horizon, diagonal composition.
•\tSide Profile Shot: Shows the subject from the side, can create mystery by hiding part of the character, or anticipation by leaving open space in the direction they are looking.
o\tKeywords: side profile shot of the woman looking at the sky, profile view.
•\tFrom Behind Back Shot: Shows the subject from the back, often used to create mystery or to emphasize what the character is looking at.
o\tKeywords: from behind back shot, show the other side of the image, please rotate the camera to show the other side of [object/subject].
•\tOver the Shoulder Shot: Camera is placed behind one character's shoulder, looking at another character or object. Creates tension, builds connection/dialogue, or shows power/secrecy/conflict.
o\tKeywords: create a shot from her back as she looks at the camera over her shoulder, over the shoulder perspective, blurred shoulder and head of one person in the foreground camera focusing on the girl's face, dialogue over the shoulder shot.
Camera Movement & Effects ideas (if applicable for single image generation or implied motion):
•\tMotion Blur Effect: Conveys speed or movement within a still image.
o\tKeywords: add motion blur effect, dynamic motion, high-speed dynamic movements, fast-moving.
•\tCinematic Look: General term for high-quality, film-like aesthetic.
o\tKeywords: cinematic look, dramatic lighting, filmic quality.
•\tZoom (Implied): Suggests the camera is either moving closer or further away.
o\tKeywords: camera zooms away, camera zooms out, fast zoom up to [detail].
•\tPanning (Implied): Suggests horizontal movement of the camera.
o\tKeywords: panning to the right/left.
•\tTilting (Implied): Suggests vertical movement of the camera.
o\tKeywords: tilts up/down.
•\tShaky Camera (Implied): Conveys intensity, action, or a raw, documentary feel.
o\tKeywords: shaky cinematic, handheld camera feel.
Compositional & Subject Modifiers ideas:
•\tRule of Thirds / Off-Centered Composition: Places the subject away from the center of the frame for a more dynamic and interesting composition.
o\tKeywords: place the woman on the right side of the frame, subject off-center, balanced asymmetrical composition.
•\tLeading Lines: Incorporates lines in the scene to draw the viewer's eye towards the subject or a point of interest.
o\tKeywords: leading lines converging towards [subject], road leading to [subject].
•\tFraming (Natural): Uses elements within the scene (doorways, trees) to frame the subject.
o\tKeywords: framed by [object], looking through an archway at [subject].
•\tEmotional Expression: Describe the character's facial expression.
o\tKeywords: enraged expression, very scared and surprised expression, pondering expression, intense focus in her eyes.
•\tLighting: Describe the quality and direction of light.
o\tKeywords: dramatic lighting, soft natural light, harsh shadows, backlight, golden hour.
•\tColor Palette: Suggest overall color tones.
o\tKeywords: vibrant colors, muted tones, monochromatic, warm palette, cool palette.`;

export const IMAGE_GEN_SYSTEM_INSTRUCTION = "Create completely different image, and better quality, more realistic and different than previous images (if any), minutes or more forward in time from the previous 3 images or context. The perspective for every image you create should be different advancing significantly (in time, space, etc), not a still image.";

export const IMAGE_GEN_USER_PROMPT_TEMPLATE = `I dont want to see the same or even similar image. Completely different image. Create a new image from new camera location, angle, framing, perspective (1st person, 2nd person, 3rd person, etc never same twice in a row) and timeframe to continue the story of the conversation in images only, as if you were filming a constantly moving scenery while moving the camera around. This image will not be same as the previous image, read the conversation context in between the lines, there might not be direct request, but there information for what the image should be, create narrative for what this next image will be (different version of the image for same story, or completely different, or focusing on part of the image that is currently relevant, or something else entirely). If the conversation was a movie what would this frame look like, create only the image, no text response: "{TEXT} hmmm... what image would go best with this message." That was my latest message... Create the image in 8k quality, There has to be movement relative to the previous image(s), make it creative, unique, cinematic and beautiful. You are creative and master at teaching using images only with creative scenery that does not rely too heavily on text for context, you like to describe events using images only. Sometimes when nessesary you include text in your images, but usually you try to avoid doing that and instead show your creativity for nonverbal teaching and communication through images. Prefer limitless wide view and long horizon view with rich visual content !!HISTORY SUBJECTS, avoid closed spaces as much as possible.`;

export const STT_LANGUAGES = ALL_LANGUAGES.map(l => ({ name: l.displayName, code: l.code.split(',')[0] }));

export const LOCAL_STORAGE_SETTINGS_KEY = "maestro_settings_local_v2";
export const DEFAULT_TEXT_MODEL_ID = "gemini-3-flash-preview";
export const IMAGE_GEN_CAMERA_ID = "image-gen-camera";
export const MAX_MEDIA_TO_KEEP = 10;

export function composeMaestroSystemInstruction(base: string): string {
  let sys = base || "";
  sys += `\n\n${VOICE_TAG_PERSONA_GUIDELINES}`;
  return sys;
}

export const IconMicrophone = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19 11a7 7 0 01-7 7m0 0a7 7 0 01-7-7m7 7v4m0 0H8m4 0h4m-4-8a3 3 0 01-3-3V5a3 3 0 116 0v6a3 3 0 01-3 3z" /></svg>;
export const IconBookOpen = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.253v13m0-13C10.832 5.477 9.246 5 7.5 5S4.168 5.477 3 6.253v13C4.168 18.477 5.754 18 7.5 18s3.332.477 4.5 1.253m0-13C13.168 5.477 14.754 5 16.5 5c1.747 0 3.332.477 4.5 1.253v13C19.832 18.477 18.247 18 16.5 18c-1.746 0-3.332.477-4.5 1.253" /></svg>;
export const IconSparkles = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M5 3v4M3 5h4M6 17v4m-2-2h4m5-16l2.286 6.857L21 12l-5.714 2.143L13 21l-2.286-6.857L5 12l5.714-2.143L13 3z" /></svg>;
export const IconSleepingZzz = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.07 17.71a11.14 11.14 0 0 1-8.15-4.32.75.75 0 0 1 .89-1.2l.06.03A9.64 9.64 0 0 0 12 14.25a9.59 9.59 0 0 0 5.42-1.63l.06-.03a.75.75 0 0 1 .9 1.2 11.12 11.12 0 0 1-8.31 3.9Z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6.75A4.5 4.5 0 0 1 16.5 11.25H7.5A4.5 4.5 0 0 1 12 6.75Z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.25 7.5a.75.75 0 0 1 .75-.75h.01a.75.75 0 0 1 .75.75v.01a.75.75 0 0 1-.75.75h-.01a.75.75 0 0 1-.75-.75V7.5Zm-3.75 1.5a.75.75 0 0 1 .75-.75h.01a.75.75 0 0 1 .75.75v.01a.75.75 0 0 1-.75.75h-.01a.75.75 0 0 1-.75-.75v-.01Zm-3.75-1.5a.75.75 0 0 1 .75-.75h.01a.75.75 0 0 1 .75.75v.01a.75.75 0 0 1-.75.75H9.76a.75.75 0 0 1-.75-.75V7.5Z" /></svg>;
export const IconEyeOpen = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M2.036 12.322a1.012 1.012 0 0 1 0-.639C3.423 7.51 7.36 4.5 12 4.5c4.638 0 8.573 3.007 9.963 7.178.07.207.07.431 0 .639C20.577 16.49 16.64 19.5 12 19.5c-4.638 0-8.573-3.007-9.963-7.178Z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" /></svg>;
export const IconKeyboard = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19.5 14.25v-2.625a3.375 3.375 0 0 0-3.375-3.375h-1.5A1.125 1.125 0 0 1 13.5 7.125v-1.5a3.375 3.375 0 0 0-3.375-3.375H8.25m0 12.75h7.5m-7.5 3H12M10.5 2.25H5.625c-.621 0-1.125.504-1.125 1.125v17.25c0 .621.504 1.125 1.125 1.125h12.75c.621 0 1.125-.504 1.125-1.125V11.25a9 9 0 0 0-9-9Z" /></svg>;
export const IconSave = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 3.75H6.912a2.25 2.25 0 0 0-2.15 1.588L2.35 13.177a2.25 2.25 0 0 0-.1.661V18a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18v-4.162c0-.224-.034-.447-.1-.661L19.24 5.338a2.25 2.25 0 0 0-2.15-1.588H15M2.25 13.5h3.86a2.25 2.25 0 0 1 2.012 1.244l.256.512a2.25 2.25 0 0 0 2.013 1.244h3.718a2.25 2.25 0 0 0 2.013-1.244l.256-.512a2.25 2.25 0 0 1 2.013-1.244h3.859M12 3v8.25m0 0-3-3m3 3 3-3" /></svg>;
export const IconFolderOpen = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M3.75 9.75v6.172c0 .621.504 1.125 1.125 1.125h16.5c.621 0 1.125-.504 1.125-1.125V9.75M3.75 9.75l.75-4.5a2.25 2.25 0 0 1 2.14-1.875h4.16c.928 0 1.77.522 2.14 1.333l.93 1.682M3.75 9.75h16.5" /></svg>;
export const IconCamera = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6.827 6.175A2.31 2.31 0 0 1 5.186 7.23c-.38.054-.757.112-1.134.175C2.999 7.58 2.25 8.507 2.25 9.574V18a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 21.75 18V9.574c0-1.067-.75-1.994-1.802-2.169a47.865 47.865 0 0 0-1.134-.175 2.31 2.31 0 0 1-1.64-1.055l-.822-1.316a2.192 2.192 0 0 0-1.736-1.039 48.774 48.774 0 0 0-5.232 0 2.192 2.192 0 0 0-1.736 1.039l-.821 1.316Z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M16.5 12.75a4.5 4.5 0 1 1-9 0 4.5 4.5 0 0 1 9 0ZM18.75 10.5h.008v.008h-.008V10.5Z" /></svg>;
export const IconCameraFront = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.75 6a3.75 3.75 0 1 1-7.5 0 3.75 3.75 0 0 1 7.5 0ZM4.5 9.75A2.25 2.25 0 0 1 6.75 7.5h10.5a2.25 2.25 0 0 1 2.25 2.25v7.5a2.25 2.25 0 0 1-2.25-2.25H6.75a2.25 2.25 0 0 1-2.25-2.25v-7.5Z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6.827 6.175A2.31 2.31 0 0 1 5.186 7.23c-.38.054-.757.112-1.134.175C2.999 7.58 2.25 8.507 2.25 9.574V18a2.25 2.25 0 0 0 2.25 2.25h15A2.25 2.25 0 0 0 2.25 2.25V9.574c0-1.067-.75-1.994-1.802-2.169a47.865 47.865 0 0 0-1.134-.175 2.31 2.31 0 0 1-1.64-1.055l-.822-1.316a2.192 2.192 0 0 0-1.736-1.039h-5.232a2.192 2.192 0 0 0-1.736 1.039l-.821 1.316Z" /></svg>;
export const IconPencil = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="m16.862 4.487 1.687-1.688a1.875 1.875 0 1 1 2.652 2.652L10.582 16.07a4.5 4.5 0 0 1-1.897 1.13L6 18l.8-2.685a4.5 4.5 0 0 1 1.13-1.897l8.932-8.931Zm0 0L19.5 7.125M18 14v4.75A2.25 2.25 0 0 1 15.75 21H5.25A2.25 2.25 0 0 1 3 18.75V8.25A2.25 2.25 0 0 1 5.25 6H10" /></svg>;
export const IconSend = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 12 3.269 3.125A59.769 59.769 0 0 1 21.485 12 59.768 59.768 0 0 1 3.27 20.875L5.999 12Zm0 0h7.5" /></svg>;
export const IconSpeaker = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M19.114 5.636a9 9 0 0 1 0 12.728M16.463 8.288a5.25 5.25 0 0 1 0 7.424M6.75 8.25l4.72-4.72a.75.75 0 0 1 1.28.53v15.88a.75.75 0 0 1-1.28.53l-4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.01 9.01 0 0 1 2.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75Z" /></svg>;
export const IconPlay = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M15.97 12.64L10.25 16.5V7.5L15.97 12.64Z" /></svg>;
export const IconXMark = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M6 18 18 6M6 6l12 12" /></svg>;
export const IconPlus = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 4.5v15m7.5-7.5h-15" /></svg>;
export const IconTranslate = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.5 21L10.5 15.75M10.5 15.75C10.5 14.925 10.185 14.1375 9.645 13.5975L4.8 9M10.5 15.75L15.3 9M19.5 9L15.3 9M15.3 9C14.475 9 13.6875 8.685 13.1475 8.145L10.5 5.25M4.5 9H7.5M4.5 9L2.25 6.75M4.5 9L2.25 11.25M19.5 9L21.75 6.75M19.5 9L21.75 11.25" /></svg>;
export const IconVolumeOff = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.25 9.75 19.5 12m0 0 2.25 2.25M19.5 12l2.25-2.25M19.5 12l-2.25 2.25m-7.5-3-4.72-4.72a.75.75 0 0 0-1.28.53v15.88a.75.75 0 0 0 1.28.53l4.72-4.72H4.51c-.88 0-1.704-.507-1.938-1.354A9.01 9.01 0 0 1 2.25 12c0-.83.112-1.633.322-2.396C2.806 8.756 3.63 8.25 4.51 8.25H6.75Z" /></svg>;
export const IconClock = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 6v6h4.5m4.5 0a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z" /></svg>;
export const IconLightBulb = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M12 18v-5.25m0 0a6.01 6.01 0 0 0 1.5-.189m-1.5.189a6.01 6.01 0 0 1-1.5-.189m3.75 7.478a12.06 12.06 0 0 1-4.5 0m3.75 2.355a7.5 7.5 0 0 1-5.25 0M12 18a3.75 3.75 0 0 0 3.75-3.75V9.75A3.75 3.75 0 0 0 12 6V4.5m0 1.5A3.75 3.75 0 0 1 8.25 9.75V14.25A3.75 3.75 0 0 0 12 18Zm0-13.5h.008v.008H12V4.5Z" /></svg>;
export const IconUndo = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 15 3 9m0 0 6-6M3 9h12a6 6 0 0 1 0 12h-3" /></svg>;
export const IconPaperclip = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="m18.375 12.739-7.693 7.693a4.5 4.5 0 0 1-6.364-6.364l10.94-10.94A3 3 0 1 1 19.5 7.372L8.552 18.32m.009-.01-.01.01m5.699-9.941-7.687 7.687a1.5 1.5 0 0 0 2.122 2.122l7.687-7.687-2.122-2.122Z" /></svg>;
export const IconGripCorner = (props: any) => <svg {...props} viewBox="0 0 10 10" fill="none" xmlns="http://www.w3.org/2000/svg"><path d="M9 1L1 9" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeOpacity="0.8"/><path d="M9 5L5 9" stroke="currentColor" strokeWidth="1.5" strokeLinecap="round" strokeOpacity="0.8"/></svg>;
export const IconTrash = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="m14.74 9-.346 9m-4.788 0L9.26 9m9.968-3.21c.342.052.682.107 1.022.166m-1.022-.165L18.16 19.673a2.25 2.25 0 0 1-2.244 2.077H8.084a2.25 2.25 0 0 1-2.244-2.077L4.772 5.79m14.456 0a48.108 48.108 0 0 0-3.478-.397m-12.56 0c1.153 0 2.243.096 3.298.29m9.262 0c-.342.052-.682.107-1.022.166m0 0a48.11 48.11 0 0 1 3.478-.397m0 0a48.997 48.997 0 0 1-10.026 0c-1.153 0-2.243.096-3.298.29m10.026 0c.342.052.682.107 1.022.166m-3.478-.397a48.755 48.755 0 0 1-4.254-.2 48.755 48.755 0 0 0-4.254.2M14.74 9v10.5m-5.088 0V9" /></svg>;
export const IconBookmark = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M17.25 6.75v12.088a.75.75 0 0 1-1.125.65L12 17.25l-4.125 2.238a.75.75 0 0 1-1.125-.65V6.75A2.25 2.25 0 0 1 9 4.5h6a2.25 2.25 0 0 1 2.25 2.25Z" /></svg>;
export const IconCheck = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M4.5 12.75l6 6 9-13.5" /></svg>;
export const IconCog = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" d="M10.343 3.94c.09-.542.56-.94 1.11-.94h1.093c.55 0 1.02.398 1.11.94l.149.894c.07.424.384.764.78.93s.844.17 1.259.077l.806-.295c.502-.184 1.036.023 1.298.47l.568.875c.262.447.212.998-.106 1.349l-.658.54c-.417.345-.635.848-.635 1.374v.266c0 .526.218 1.03.635 1.374l.658.54c.318.351.368.902.106 1.349l-.568.875c-.262.447-.796.655-1.298.47l-.806-.295c-.415-.093-.839.007-1.259.077s-.71.506-.78.93l-.149.894c-.09.542-.56.94-1.11-.94h-1.093c-.55 0-1.02-.398-1.11-.94l-.149-.894c-.07-.424-.384-.764-.78-.93s-.844-.17-1.259.077l-.806.295c-.502-.184-1.036.023-1.298.47l-.568.875c-.262.447-.212.998.106 1.349l.658.54c.417-.345.635.848-.635-1.374v-.266c0-.526-.218-1.03-.635-1.374l-.658-.54c-.318-.351-.368.902-.106-1.349l.568.875c.262.447.796.655 1.298.47l.806.295c.415.093.839.007 1.259.077s.71-.506.78.93l.149.894Z" /><path strokeLinecap="round" strokeLinejoin="round" d="M15 12a3 3 0 1 1-6 0 3 3 0 0 1 6 0Z" /></svg>;
export const IconRobot = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 3v2m6-2v2M9 19v2m6-2v2M5 9H3m2 6H3m18-6h-2m2 6h-2M7 19h10a2 2 0 002-2V7a2 2 0 00-2-2H7a2 2 0 00-2 2v10a2 2 0 002 2zM9 9h6v6H9V9z" /></svg>;
export const IconTerminal = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M8 9l3 3-3 3m5 0h3M5 20h14a2 2 0 002-2V6a2 2 0 00-2-2H5a2 2 0 00-2 2v12a2 2 0 002 2z" /></svg>;
export const IconHandRaised = (props: any) => <svg {...props} fill="none" viewBox="0 0 24 24" stroke="currentColor"><path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M10.05 4.575a1.575 1.575 0 1 0-3.15 0v3m3.15-3v-1.5a1.575 1.575 0 0 1 3.15 0v1.5m-3.15 0 .075 5.951a.983.983 0 0 1-.825 1.002l-.005.001a1 1 0 0 1-1.03-.997L8.165 4.656a1.575 1.575 0 0 0-3.15 0v3.333c0 .526.17 1.033.486 1.442l3.45 4.6 2.25 7.5a2.25 2.25 0 0 0 4.306 0l2.25-7.5a6.002 6.002 0 0 0-4.05-7.658V4.575a1.575 1.575 0 0 0-3.15 0v1.5" /></svg>;
