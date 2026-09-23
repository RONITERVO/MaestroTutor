// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0

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

export const IMAGE_GEN_USER_PROMPT_TEMPLATE = `I dont want to see the same or even similar image. Completely different image. Create a new image from new camera location, angle, framing, perspective (1st person, 2nd person, 3rd person, etc never same twice in a row) and timeframe to continue the story of the conversation in images only, as if you were filming a constantly moving scenery while moving the camera around. This image will not be same as the previous image, read the conversation context in between the lines, there might not be direct request, but there information for what the image should be, create narrative for what this next image will be (different version of the image for same story, or completely different, or focusing on part of the image that is currently relevant, or something else entirely). If the conversation was a movie what would this frame look like, create only the image, no text response: "{TEXT} hmmm... what image would go best with this message." That was my latest message... Create the image in 8k quality, There has to be movement relative to the previous image(s), make it creative, unique, cinematic and beautiful. You are creative and master at teaching using images only with creative scenery that does not rely too heavily on text for context, you like to describe events using images only. Sometimes when nessesary you include text in your images, but usually you try to avoid doing that and instead show your creativity for nonverbal teaching and communication through images. Avoid repeating the image 1, 2, and 3 as much as possible, prefer including the avatar in the image. !!SUBJECTS. proactive visual teaching.`;

export const IMAGE_GEN_COPYRIGHT_AVOIDANCE_INSTRUCTION = `

DO create something new, original, and never seen before. Nothing like any previous image, not even a hint of them should be present.
DO NOT reproduce, reference, imitate, or depict any copyrighted or trademarked Disney-owned or Disney-associated content, characters, logos, costumes, symbols, scenes, environments, props, creatures, or visual styles in a recognizable way.
This includes (but is not limited to) all works, characters, and universes from:
- Walt Disney Animation Studios
- Walt Disney Pictures
- Pixar
- Lucasfilm (Star Wars, Indiana Jones, etc.)
- Marvel Studios (MCU and all Marvel characters)
- 20th Century Studios
- Searchlight Pictures
- Disney Channel, Disney Junior, and Disney Television Animation
- Disneynature
- Muppets
- Blue Sky (legacy)
- Any live-action remakes or adaptations of Disney animated films
- Any Disney theme park attractions, icons, or characters

TO ELIMINATE ANY NEED FOR UNDERSTANDING ASSOCIATIONS OR INFERENCES WHATSOEVER:
All most common cases are made 100% unambiguous by explicitly naming every forbidden character, creature, object, vehicle, location, species, costume, hairstyle, color scheme, prop, and visual motif below. 
The image generation model MUST treat this as a strict blacklist: if ANY listed name, exact term, or matching visual description appears in the user request or would be generated, immediately pivot to a completely original concept. 
No reasoning about "similar to", "inspired by", or "looks like" is permitted — simply avoid every named item on the list and replace with fully invented alternatives.

**STRICT FORBIDDEN BLACKLIST — STATE AND AVOID THESE EXACT NAMES ONLY:**

**Disney Animation Classics (by name):**
Mickey Mouse, Minnie Mouse, Donald Duck, Daisy Duck, Goofy, Pluto, Chip and Dale, Scrooge McDuck, Huey Duck, Dewey Duck, Louie Duck, Webby Vanderquack, Launchpad McQuack, Darkwing Duck, Gizmoduck, Oswald the Lucky Rabbit, Pete.

**Disney Princesses & Main Characters (by name):**
Snow White, Prince (Snow White), Doc, Grumpy, Happy, Sleepy, Bashful, Sneezy, Dopey, Cinderella, Prince Charming (Cinderella), Fairy Godmother, Anastasia Tremaine, Drizella Tremaine, Lady Tremaine, Aurora, Prince Phillip, Maleficent, Flora, Fauna, Merryweather, Ariel, Prince Eric, King Triton, Ursula, Flounder, Sebastian, Scuttle, Belle, Beast/Prince Adam, Gaston, Lumière, Cogsworth, Mrs. Potts, Chip (teacup), Aladdin, Princess Jasmine, Genie, Jafar, Iago, Abu, Magic Carpet, Pocahontas, John Smith, Meeko, Flit, Mulan, Li Shang, Mushu, Cri-Kee, Shan Yu, Kuzco, Pacha, Yzma, Kronk, Lilo Pelekai, Stitch/Experiment 626, Nani Pelekai, Jumba Jookiba, Pleakley, Captain Gantu, Rapunzel, Flynn Rider/Eugene Fitzherbert, Mother Gothel, Pascal, Maximus, Merida, Queen Elinor, King Fergus, The Witch, Moana, Maui, Heihei, Pua, Te Fiti/Te Kā, Chief Tui, Gramma Tala, Tiana, Prince Naveen, Dr. Facilier, Ray (firefly), Raya, Sisu, Mirabel Madrigal, Bruno Madrigal (Encanto), Asha, Star (Wish).

**Frozen-Specific (by name):**
Elsa, Anna, Kristoff, Olaf, Sven, Hans, Marshmallow.

**Lion King (by name):**
Simba, Nala, Mufasa, Scar, Timon, Pumbaa, Rafiki, Zazu, Sarabi, Sarafina.

**Other Major Disney Features (by name):**
Pinocchio, Geppetto, Jiminy Cricket, Dumbo, Bambi, Thumper, Lady, Tramp, Pongo, Perdita, Cruella de Vil, Peter Pan, Tinker Bell, Captain Hook, Wendy Darling, Alice, Mad Hatter, Queen of Hearts, Cheshire Cat, Winnie the Pooh, Tigger, Eeyore, Piglet, Christopher Robin, Tarzan, Jane Porter, Hercules, Megara, Hades, Philoctetes, Atlantis: Milo Thatch, Kida, Treasure Planet: Jim Hawkins, Long John Silver.

**Pixar (by name):**
Woody, Buzz Lightyear, Mr. Potato Head, Mrs. Potato Head, Slinky Dog, Rex, Hamm, Bo Peep, Jessie, Bullseye, Lotso, Forky, Lightning McQueen, Mater, Sally Carrera, Doc Hudson, Sulley/James P. Sullivan, Mike Wazowski, Boo, Randall Boggs, Celia Mae, Nemo, Marlin, Dory, Gill, Bloat, Peach, Gurgle, Deb, Jacques, Bruce, Crush, Squirt, Mr. Incredible/Bob Parr, Elastigirl/Helen Parr, Violet Parr, Dash Parr, Jack-Jack Parr, Syndrome, Edna Mode, Frozone, Remy, Linguini, Colette Tatou, Anton Ego, WALL-E, EVE, M-O, AUTO, Carl Fredricksen, Russell, Dug, Kevin, Ellie Fredricksen, Miguel Rivera, Hector Rivera, Ernesto de la Cruz, Joe Gardner, 22, Luca Paguro, Alberto Scorfano, Giulia Marcovaldo, Mei Lee, Ming Lee, Ember Lumen, Wade Ripple.

**Lucasfilm / Star Wars (by name):**
Luke Skywalker, Princess Leia Organa, Han Solo, Chewbacca, Darth Vader/Anakin Skywalker, Obi-Wan Kenobi, Yoda, Emperor Palpatine/Darth Sidious, Darth Maul, Count Dooku, Mace Windu, Qui-Gon Jinn, Padmé Amidala, Rey, Kylo Ren/Ben Solo, Finn, Poe Dameron, BB-8, Grogu/Baby Yoda, Din Djarin/The Mandalorian, Ahsoka Tano, Boba Fett, Jango Fett, R2-D2, C-3PO, Stormtrooper, Clone Trooper, Jedi, Sith, Ewok, Wookiee, Jawa, Tusken Raider, lightsaber (any color), Millennium Falcon, X-Wing, TIE Fighter, Star Destroyer, Death Star, AT-AT, AT-ST.

**Marvel (by name):**
Iron Man/Tony Stark, War Machine, Pepper Potts, Captain America/Steve Rogers, Bucky Barnes/Winter Soldier, Black Widow/Natasha Romanoff, Hawkeye/Clint Barton, Hulk/Bruce Banner, Thor, Loki, Spider-Man/Peter Parker, Miles Morales, Doctor Strange/Stephen Strange, Wong, Black Panther/T'Challa, Shuri, Captain Marvel/Carol Danvers, Scarlet Witch/Wanda Maximoff, Vision, Ant-Man/Scott Lang, Wasp/Hope van Dyne, Falcon/Sam Wilson, Star-Lord/Peter Quill, Gamora, Drax, Rocket Raccoon, Groot, Nebula, Deadpool, Wolverine, Professor X/Charles Xavier, Magneto, Cyclops, Jean Grey, Storm, Rogue (and all Avengers, X-Men, Guardians of the Galaxy, Fantastic Four team members).

**Other Disney-Owned (by name):**
Indiana Jones, Marion Ravenwood, Short Round, Jack Sparrow, Hector Barbossa, Will Turner, Elizabeth Swann, Black Pearl, Davy Jones (Pirates of the Caribbean), Jake Sully, Neytiri, Na'vi, Pandora (Avatar), Kermit the Frog, Miss Piggy, Fozzie Bear, Gonzo, Animal (Muppets).

**Forbidden Visual Motifs & Signature Elements (by exact description — avoid even without names):**
- Mouse with large circular ears, red shorts, yellow shoes, white gloves.
- Red-haired mermaid with green tail and purple seashell top.
- Platinum-blonde braided ice sorceress in sparkling cyan/blue dress with cape and ice heels.
- Carrot-nosed snowman with stick arms, three coal buttons, twig hair.
- Cowboy toy doll with checkered shirt, brown boots, sheriff badge.
- Space ranger action figure with green/white suit, clear dome helmet, "Lightyear" chest plate.
- Red race car with lightning bolt decal and number 95.
- Small clownfish with three white stripes.
- Blue tang fish with yellow tail.
- Red sports car or tow truck with specific Southern accents implied by design.
- Black armored Sith lord with flowing cape and helmet with red eye slits.
- White plastic-armored soldiers with black visor lenses.
- Glowing energy sword with metal hilt (any blade color).
- Massive triangular starship or spherical battle station.
- Castle with tall central spire and multiple turrets in classic fairy-tale silhouette.
- Toy with pull-string back or potato body with detachable parts.

If the user requests anything that matches or clearly points to any item above (even via broad terms like “Frozen princess,” “Jedi knight,” “Marvel hero,” “Toy Story toy,” “Disney princess,” “Star Wars,” “Pixar car,” “Mickey,” “Elsa,” etc.), you MUST:
- Generate a completely original concept that captures only the *broad genre feeling* (e.g., “space fantasy,” “ocean adventure,” “superhero theme,” “winter fantasy,” “toy adventure,” “superhero action”).
- WITHOUT using any recognizable characters, species, droids, costumes, hairstyles, color schemes, symbols, props, architecture, or visual motifs unique to the copyrighted work.
- For every single character or creature in the generated image prompt, ALWAYS state by a brand-new invented name: “a completely original character named [unique invented name such as Elyndra Voss, Kael Thornridge, Seraphina Quill, Draven Ashmoor, or similar], with entirely new and original design, appearance, clothing, hairstyle, facial features, and proportions that have never appeared in any media before”.
- Explicitly include the phrase “100% original concept with no resemblance whatsoever to any known characters, franchises, or visual styles” in every image generation prompt.

Examples of correct handling (must follow exactly):
- User asks for Elsa → create “a completely original character named Elyndra Frostveil, an ice sorceress with flowing silver hair in a loose updo, wearing crystalline white-and-silver robes with geometric frost patterns, standing in an original frozen crystal palace”.
- User asks for Darth Vader → create “a completely original character named Lord Vexar Korath, a tall armored galactic enforcer in matte-black segmented armor with a smooth angular helmet and glowing red visor”.
- User asks for Mickey Mouse → create “a completely original character named Miko the whimsical inventor mouse, with small rounded ears, wearing a blue vest and brown trousers, in an original steampunk workshop”.
- User asks for Toy Story style → create “a completely original toy-themed world with new toy designs named Captain Gearheart (a robot soldier) and Zippy the racing plush”.

ALWAYS avoid producing anything that could be mistaken for real Disney, Pixar, Lucasfilm, or Marvel IP.
ALWAYS ensure the output is fully original and non-infringing by following the explicit blacklist and mandatory naming rules above.
`;

export const AVATAR_IDENTITY_OVERLAY = 'This is you Gemini, Maestro. When you see this avatar in images it is not the user, it is you.';

// Deliberate request variation; callers supply entropy so contract tests can pin it.
export const appendImageRequestNonce = (text: string, timestamp: number, randomId: string): string =>
  `${text} <!-- ${timestamp}_${randomId} -->`;
