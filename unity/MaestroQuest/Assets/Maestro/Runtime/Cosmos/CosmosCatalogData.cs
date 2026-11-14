using System;
using UnityEngine;

namespace Maestro.Quest.Cosmos
{
    /// <summary>
    /// Zero-GC catalog backing all 25 HyperCosmosVR documentary experiences, continuum edges,
    /// bilingual word-cue telemetry (es-ES / en-US / fi-FI), solar/galactic landmarks,
    /// App 01 chromatic prism solver, and App 02 harmonic scale table.
    /// </summary>
    public static class CosmosCatalogData
    {
        public const int ExperienceCount = 25;
        public const int TotalCatalogZones = 376;
        public const int ContinuityEdgeCount = 25;
        public const int MidiMin = 48;
        public const int MidiCount = 48;

        public static readonly CosmosExperienceMeta[] Experiences;
        public static readonly CosmosContinuityEdge[] ContinuityEdges;
        public static readonly CosmosZoneEntry[] FlagshipZones;
        public static readonly CosmosWordCue[] WordCues;
        public static readonly CosmosSolarBodyMeta[] SolarBodies;
        public static readonly CosmosGalacticLandmarkMeta[] GalacticLandmarks;
        public static readonly CosmosMusicTimbre[] MusicTimbres;
        public static readonly CosmosMusicPitch[] MusicPitches;
        public static readonly CosmosColorSample[] ColorSamples;

        static CosmosCatalogData()
        {
            var rawZones = new RawZoneSeed[]
            {
                new RawZoneSeed(3, 0, "space", new Color(0.3882f, 0.4667f, 1.0000f, 1f), new CosmosZoneSoundRecipe(122.00f, 6800.0f, 0.0150f, 0.0700f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Espacio", "Space", "Avaruus", 1120, 710, 920),
                    new CosmosBilingualText("vacío casi total", "near-total vacuum", "lähes täydellinen tyhjiö", 2160, 1710, 1850),
                    new CosmosBilingualText("La atmósfera ya se ha fundido gradualmente con el entorno interplanetario.", "The atmosphere has already gradually merged with the interplanetary environment.", "Ilmakehä on jo vähitellen sulautunut planeettojen väliseen ympäristöön.", 5080, 6160, 6440)),
                new RawZoneSeed(3, 2, "thermosphere", new Color(0.2196f, 0.8392f, 0.8706f, 1f), new CosmosZoneSoundRecipe(174.00f, 3900.0f, 0.0500f, 0.1200f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Termosfera", "Thermosphere", "Termosfääri", 780, 830, 900),
                    new CosmosBilingualText("gas muy tenue y parcialmente ionizado", "very tenuous and partially ionized gas", "erittäin ohut ja osittain ionisoitunut kaasu", 3680, 4200, 4760),
                    new CosmosBilingualText("Absorbe radiación solar energética; aquí brillan muchas auroras y orbita la estación espacial.", "It absorbs high-energy solar radiation; many auroras glow here, and the space station orbits here.", "Se absorboi suurienergistä auringonsäteilyä; täällä loistavat monet revontulet ja avaruusasema kiertää Maata.", 7600, 7960, 8960)),
                new RawZoneSeed(3, 6, "surface", new Color(0.4196f, 0.9059f, 0.6078f, 1f), new CosmosZoneSoundRecipe(118.00f, 1700.0f, 0.1600f, 0.1200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Superficie", "Surface", "Pinta", 1240, 760, 530),
                    new CosmosBilingualText("roca, suelo, agua y vida", "rock, soil, water and life", "kivi, maaperä, vesi ja elämä", 3520, 3120, 3040),
                    new CosmosBilingualText("Una frontera finísima separa la atmósfera del océano y de la Tierra sólida.", "An extremely thin boundary separates the atmosphere from the ocean and solid Earth.", "Äärimmäisen ohut raja erottaa ilmakehän valtamerestä ja kiinteästä Maasta.", 6400, 6400, 6400)),
                new RawZoneSeed(3, 7, "crust", new Color(0.8549f, 0.6196f, 0.3725f, 1f), new CosmosZoneSoundRecipe(94.00f, 1250.0f, 0.1800f, 0.1600f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Corteza", "Crust", "Maankuori", 1000, 610, 940),
                    new CosmosBilingualText("roca sólida y quebradiza", "solid and brittle rock", "kiinteä ja hauras kivi", 2080, 2280, 2400),
                    new CosmosBilingualText("Es la piel rocosa del planeta: delgada bajo los océanos y más gruesa bajo los continentes.", "It is the rocky skin of the planet: thin under the oceans and thicker under the continents.", "Se on planeetan kivinen iho: ohut valtamerten alla ja paksumpi mantereiden alla.", 7440, 7280, 7840)),
                new RawZoneSeed(3, 11, "lower-mantle", new Color(0.9333f, 0.2745f, 0.1255f, 1f), new CosmosZoneSoundRecipe(43.00f, 460.0f, 0.1800f, 0.3600f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Manto inferior", "Lower mantle", "Alempi vaippa", 1400, 1470, 1120),
                    new CosmosBilingualText("roca sólida que fluye lentamente", "solid rock that flows slowly", "kiinteä kivi, joka virtaa hitaasti", 3200, 2800, 3080),
                    new CosmosBilingualText("Es la capa más voluminosa de la Tierra y transporta calor mediante convección extremadamente lenta.", "It is Earth's largest layer by volume and transports heat through extremely slow convection.", "Se on tilavuudeltaan Maan suurin kerros ja kuljettaa lämpöä erittäin hitaan konvektion avulla.", 7600, 7480, 7200)),
                new RawZoneSeed(3, 13, "inner-core", new Color(1.0000f, 0.8980f, 0.5961f, 1f), new CosmosZoneSoundRecipe(29.00f, 250.0f, 0.0800f, 0.6200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Núcleo interno", "Inner core", "Sisäinen ydin", 1560, 890, 1210),
                    new CosmosBilingualText("hierro y níquel sólidos", "solid iron and nickel", "kiinteä rauta ja nikkeli", 2520, 2520, 2400),
                    new CosmosBilingualText("Aunque está extremadamente caliente, la presión colosal mantiene sólido el centro del planeta.", "Although it is extremely hot, immense pressure keeps the planet's center solid.", "Vaikka planeetan keskus on erittäin kuuma, valtava paine pitää sen kiinteänä.", 7120, 6480, 6120)),
                new RawZoneSeed(4, 0, "sun", new Color(1.0000f, 0.7765f, 0.2902f, 1f), new CosmosZoneSoundRecipe(196.00f, 5200.0f, 0.2200f, 0.1600f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Sol", "Sun", "Aurinko", 760, 510, 700),
                    new CosmosBilingualText("Centro del sistema solar", "Center of the solar system", "Aurinkokunnan keskus", 2280, 2400, 1660),
                    new CosmosBilingualText("Es una estrella y concentra casi toda la masa del sistema solar. Su energía hace posibles el clima y la vida en la Tierra.", "It is a star and contains almost all the mass in the solar system. Its energy makes Earth's climate and life possible.", "Se on tähti ja sisältää lähes kaiken aurinkokunnan massan. Sen energia mahdollistaa Maan ilmaston ja elämän.", 9440, 9560, 9760)),
                new RawZoneSeed(4, 1, "mercury", new Color(0.7255f, 0.6784f, 0.6157f, 1f), new CosmosZoneSoundRecipe(180.00f, 3600.0f, 0.1200f, 0.1800f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Mercurio", "Mercury", "Merkurius", 1040, 680, 950),
                    new CosmosBilingualText("Planeta rocoso", "rocky planet", "kivinen planeetta", 1720, 1070, 1470),
                    new CosmosBilingualText("Es el planeta más cercano al Sol y el más pequeño de los ocho planetas.", "It is the closest planet to the Sun and the smallest of the eight planets.", "Se on Aurinkoa lähinnä oleva planeetta ja pienin kahdeksasta planeetasta.", 5080, 5200, 6760)),
                new RawZoneSeed(4, 2, "venus", new Color(1.0000f, 0.7451f, 0.3922f, 1f), new CosmosZoneSoundRecipe(160.00f, 2850.0f, 0.1800f, 0.2000f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Venus", "Venus", "Venus", 1000, 710, 760),
                    new CosmosBilingualText("Planeta rocoso", "rocky planet", "kivinen planeetta", 1720, 1070, 1470),
                    new CosmosBilingualText("Aunque no es el más cercano al Sol, es el planeta más caliente por su intenso efecto invernadero.", "Although it is not the closest to the Sun, it is the hottest planet due to its intense greenhouse effect.", "Vaikka se ei ole Aurinkoa lähinnä oleva planeetta, se on voimakkaan kasvihuoneilmiön vuoksi kuumin.", 7600, 7120, 7200)),
                new RawZoneSeed(4, 3, "earth", new Color(0.3451f, 0.8000f, 1.0000f, 1f), new CosmosZoneSoundRecipe(146.00f, 2400.0f, 0.1400f, 0.1200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Tierra", "Earth", "Maapallo", 680, 360, 780),
                    new CosmosBilingualText("Planeta rocoso · punto de continuidad", "Rocky planet · point of continuity", "Kiviplaneetta · jatkuvuuspiste", 3600, 3120, 2790),
                    new CosmosBilingualText("Aquí termina la exploración de la atmósfera de la app anterior y comienza el mapa completo del sistema solar.", "Here the exploration of the atmosphere in the previous app ends and the complete map of the solar system begins.", "Täällä ilmakehän tutkiminen edellisessä sovelluksessa päättyy ja aurinkokunnan täydellinen kartta alkaa.", 8000, 8000, 8240)),
                new RawZoneSeed(4, 4, "mars", new Color(0.9333f, 0.4392f, 0.2745f, 1f), new CosmosZoneSoundRecipe(111.00f, 1950.0f, 0.1600f, 0.1800f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Marte", "Mars", "Mars", 490, 660, 610),
                    new CosmosBilingualText("Planeta rocoso", "rocky planet", "kivinen planeetta", 1720, 1070, 1470),
                    new CosmosBilingualText("Es un desierto frío con volcanes gigantes, cañones profundos y señales de antiguos ríos y lagos.", "It is a cold desert with giant volcanoes, deep canyons, and signs of ancient rivers and lakes.", "Se on kylmä autiomaa, jossa on jättimäisiä tulivuoria, syviä kanjoneita ja merkkejä muinaisista joista ja järvistä.", 8960, 9680, 10640)),
                new RawZoneSeed(4, 5, "asteroid-belt", new Color(0.7725f, 0.6745f, 0.5608f, 1f), new CosmosZoneSoundRecipe(94.00f, 1550.0f, 0.2000f, 0.2400f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Cinturón de asteroides", "asteroid belt", "asteroidivyöhyke", 1960, 950, 1620),
                    new CosmosBilingualText("Aprox. 2,1–3,3 UA del Sol", "Approx. 2.1–3.3 AU from the Sun", "Noin 2,1–3,3 AU Auringosta", 5320, 4800, 6000),
                    new CosmosBilingualText("Es una región enorme y poco densa: una nave puede atravesarla sin acercarse a ningún asteroide grande.", "It is a huge and sparse region: a ship can pass through it without approaching any large asteroid.", "Se on valtava ja harva alue: alus voi kulkea sen läpi lähestymättä mitään suurta asteroidia.", 8280, 7480, 7560)),
                new RawZoneSeed(4, 6, "jupiter", new Color(0.9137f, 0.7373f, 0.5373f, 1f), new CosmosZoneSoundRecipe(72.00f, 980.0f, 0.2200f, 0.3100f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Júpiter", "Jupiter", "Jupiter", 670, 620, 830),
                    new CosmosBilingualText("Gigante gaseoso", "gas giant", "kaasujättiläinen", 2080, 1200, 1180),
                    new CosmosBilingualText("El planeta más grande reúne más masa que todos los demás planetas juntos.", "The largest planet packs more mass than all the other planets combined.", "Suurin planeetta sisältää enemmän massaa kuin kaikki muut planeetat yhteensä.", 6600, 5280, 5720)),
                new RawZoneSeed(4, 7, "saturn", new Color(0.9569f, 0.8549f, 0.5922f, 1f), new CosmosZoneSoundRecipe(61.00f, 780.0f, 0.1700f, 0.3600f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Saturno", "Saturn", "Saturnus", 1160, 600, 1020),
                    new CosmosBilingualText("Gigante gaseoso", "gas giant", "kaasujättiläinen", 2080, 1200, 1180),
                    new CosmosBilingualText("Sus anillos parecen sólidos desde lejos, pero están formados por incontables partículas de hielo y roca.", "Its rings appear solid from afar, but they are made of countless particles of ice and rock.", "Sen renkaat näyttävät kaukaa katsottuna kiinteiltä, mutta ne koostuvat lukemattomista jää- ja kivihiukkasista.", 8680, 7240, 8040)),
                new RawZoneSeed(4, 8, "uranus", new Color(0.5216f, 0.9059f, 0.8980f, 1f), new CosmosZoneSoundRecipe(51.00f, 620.0f, 0.1100f, 0.4200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Urano", "Uranus", "Uranus", 840, 830, 730),
                    new CosmosBilingualText("Gigante de hielo", "ice giant", "jääjättiläinen", 1600, 940, 1300),
                    new CosmosBilingualText("Gira casi acostado: su eje está inclinado unos noventa y ocho grados respecto a su órbita.", "It rotates almost on its side: its axis is tilted about ninety-eight degrees relative to its orbit.", "Se pyörii lähes kyljellään: sen akseli on kallistunut noin 98 astetta suhteessa kiertorataan.", 7920, 7560, 8560)),
                new RawZoneSeed(4, 9, "neptune", new Color(0.2824f, 0.4353f, 1.0000f, 1f), new CosmosZoneSoundRecipe(43.00f, 500.0f, 0.1300f, 0.4800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Neptuno", "Neptune", "Neptunus", 710, 670, 830),
                    new CosmosBilingualText("Gigante de hielo", "ice giant", "jääjättiläinen", 1600, 940, 1300),
                    new CosmosBilingualText("Es el planeta más lejano del Sol y posee algunos de los vientos más rápidos medidos en el sistema solar.", "It is the farthest planet from the Sun and has some of the fastest winds measured in the solar system.", "Se on Aurinkoa kauimpana oleva planeetta, ja sillä puhaltaa joitakin aurinkokunnan nopeimmista mitatuista tuulista.", 8200, 7320, 9160)),
                new RawZoneSeed(4, 10, "kuiper", new Color(0.6431f, 0.7059f, 0.8706f, 1f), new CosmosZoneSoundRecipe(35.00f, 390.0f, 0.0800f, 0.5500f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Cinturón de Kuiper", "Kuiper Belt", "Kuiperin vyöhyke", 1840, 830, 1320),
                    new CosmosBilingualText("Aprox. 30–50 UA del Sol", "Approx. 30–50 AU from the Sun", "Noin 30–50 AU:n etäisyydellä Auringosta", 4240, 3880, 5480),
                    new CosmosBilingualText("Más allá de Neptuno orbitan innumerables cuerpos helados: restos conservados de la formación planetaria.", "Beyond Neptune orbit countless icy bodies: preserved remains of planetary formation.", "Neptunuksen ulkopuolella kiertää lukemattomia jäisiä kappaleita: planeettojen muodostumisen säilyneitä jäänteitä.", 8000, 7800, 8560)),
                new RawZoneSeed(4, 11, "pluto", new Color(0.9216f, 0.7647f, 0.6471f, 1f), new CosmosZoneSoundRecipe(29.00f, 310.0f, 0.0450f, 0.6400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Plutón", "Pluto", "Pluto", 880, 490, 560),
                    new CosmosBilingualText("Planeta enano · cinturón de Kuiper", "Dwarf planet · Kuiper Belt", "Kääpiöplaneetta · Kuiperin vyöhyke", 3320, 2120, 3400),
                    new CosmosBilingualText("Es un mundo activo de hielos móviles, montañas de agua congelada y una atmósfera estacional.", "It is an active world of moving ices, mountains of water ice, and a seasonal atmosphere.", "Se on aktiivinen maailma, jossa on liikkuvia jäitä, vesijäästä muodostuneita vuoria ja vuodenaikojen mukaan muuttuva ilmakehä.", 7160, 7560, 11000)),
                new RawZoneSeed(5, 0, "frontier", new Color(0.9647f, 0.7686f, 0.5686f, 1f), new CosmosZoneSoundRecipe(82.00f, 1250.0f, 0.0900f, 0.3400f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Frontera planetaria", "Planetary boundary", "Planeettakunnan raja", 2000, 1590, 1470),
                    new CosmosBilingualText("Más allá de Plutón · unos 40 UA del Sol", "Beyond Pluto · about 40 AU from the Sun", "Pluton ulkopuolella · noin 40 AU:n päässä Auringosta", 4880, 4680, 4920),
                    new CosmosBilingualText("Aquí termina el mapa planetario anterior. El Sol ya parece una estrella muy brillante, pero su influencia todavía llena el espacio.", "This is where the previous planetary map ends. The Sun already seems like a very bright star, but its influence still fills space.", "Tähän päättyy edellinen planeettakartta. Aurinko näyttää jo hyvin kirkkaalta tähdeltä, mutta sen vaikutus täyttää silti avaruuden.", 10280, 10840, 9000)),
                new RawZoneSeed(5, 1, "heliosphere", new Color(0.4039f, 0.8078f, 1.0000f, 1f), new CosmosZoneSoundRecipe(74.00f, 1050.0f, 0.1500f, 0.4000f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Heliosfera", "Heliosphere", "Heliosfääri", 1360, 900, 1190),
                    new CosmosBilingualText("Burbuja del viento solar · frontera variable", "Solar-wind bubble · variable boundary", "Aurinkotuulen kupla · muuttuva raja", 4040, 3840, 3680),
                    new CosmosBilingualText("El viento solar forma una enorme burbuja alrededor del Sol. Su borde cambia de forma según el medio interestelar.", "The solar wind forms a huge bubble around the Sun. Its edge changes shape depending on the interstellar medium.", "Aurinkotuuli muodostaa valtavan kuplan Auringon ympärille. Sen reuna muuttaa muotoaan tähtienvälisen väliaineen mukaan.", 8360, 9120, 9280)),
                new RawZoneSeed(5, 4, "alpha-centauri", new Color(1.0000f, 0.5765f, 0.4196f, 1f), new CosmosZoneSoundRecipe(98.00f, 1450.0f, 0.0600f, 0.2400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Alfa Centauri", "Alpha Centauri", "Alfa Centauri", 1240, 1160, 1530),
                    new CosmosBilingualText("Próxima Centauri · 4,24 años luz", "Proxima Centauri · 4.24 light-years", "Proxima Centauri · 4,24 valovuotta", 4680, 4200, 5320),
                    new CosmosBilingualText("El sistema estelar más cercano al Sol contiene tres estrellas. Próxima Centauri es la más cercana de todas.", "The star system closest to the Sun contains three stars. Proxima Centauri is the closest of all.", "Aurinkoa lähimpänä oleva tähtijärjestelmä sisältää kolme tähteä. Proxima Centauri on lähin kaikista.", 7880, 7320, 7480)),
                new RawZoneSeed(5, 7, "orion-spur", new Color(0.4824f, 0.7216f, 1.0000f, 1f), new CosmosZoneSoundRecipe(139.00f, 2100.0f, 0.1000f, 0.1500f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Espolón de Orión", "Orion Spur", "Orionin haara", 1760, 1030, 1250),
                    new CosmosBilingualText("Nuestra dirección dentro del disco galáctico", "Our direction within the galactic disk", "Sijaintimme galaksin kiekossa", 3720, 3120, 2740),
                    new CosmosBilingualText("El sistema solar vive en una estructura menor situada entre dos grandes brazos espirales de la Vía Láctea.", "The solar system lies in a smaller structure between two major spiral arms of the Milky Way.", "Aurinkokunta sijaitsee Linnunradan kahden suuren kierrehaaran välisessä pienemmässä rakenteessa.", 6480, 6920, 6800)),
                new RawZoneSeed(5, 8, "stellar-nursery", new Color(0.8824f, 0.4431f, 1.0000f, 1f), new CosmosZoneSoundRecipe(154.00f, 2500.0f, 0.1500f, 0.1300f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Guardería estelar", "stellar nursery", "Tähtien syntyalue", 2040, 1730, 1660),
                    new CosmosBilingualText("Nebulosas, cúmulos y estrellas recién nacidas", "Nebulae, clusters and newborn stars", "Tähtisumuja, tähtijoukkoja ja vastasyntyneitä tähtiä", 4680, 4040, 5200),
                    new CosmosBilingualText("En las nubes más densas de los brazos galácticos, la gravedad transforma gas y polvo en nuevas generaciones de estrellas.", "In the densest clouds of the galactic arms, gravity transforms gas and dust into new generations of stars.", "Galaksin kierrehaarojen tiheimmissä pilvissä painovoima muuttaa kaasua ja pölyä uusiksi tähtisukupolviksi.", 9000, 8800, 9120)),
                new RawZoneSeed(5, 9, "milky-way-disk", new Color(0.6431f, 0.6157f, 1.0000f, 1f), new CosmosZoneSoundRecipe(168.00f, 2850.0f, 0.1200f, 0.1100f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Disco galáctico", "galactic disk", "Galaksin kiekko", 1840, 1120, 1490),
                    new CosmosBilingualText("La Vía Láctea mide alrededor de 100.000 años luz de ancho", "The Milky Way is about 100,000 light-years wide", "Linnunrata on noin 100 000 valovuotta leveä", 4880, 4600, 4040),
                    new CosmosBilingualText("La mayor parte de las estrellas visibles, el gas y el polvo forman un disco aplanado con una barra central y brazos espirales.", "Most of the visible stars, gas and dust form a flattened disk with a central bar and spiral arms.", "Suurin osa näkyvistä tähdistä, kaasusta ja pölystä muodostaa litteän kiekon, jossa on keskussauva ja kierrehaaroja.", 9000, 8200, 9920)),
                new RawZoneSeed(5, 10, "galactic-center", new Color(1.0000f, 0.7451f, 0.3608f, 1f), new CosmosZoneSoundRecipe(184.00f, 3400.0f, 0.1800f, 0.0900f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Centro galáctico", "galactic center", "Galaksin keskus", 1880, 1480, 1410),
                    new CosmosBilingualText("A unos 26.000 años luz del Sol", "About 26,000 light-years from the Sun", "Noin 26 000 valovuotta Auringosta", 4040, 3760, 3840),
                    new CosmosBilingualText("El corazón de la Vía Láctea está lleno de estrellas, gas caliente y fuertes campos magnéticos, oculto tras nubes de polvo.", "The heart of the Milky Way is full of stars, hot gas and strong magnetic fields, hidden behind clouds of dust.", "Linnunradan sydän on täynnä tähtiä, kuumaa kaasua ja voimakkaita magneettikenttiä pölypilvien takana.", 9760, 9240, 8200)),
                new RawZoneSeed(5, 12, "galactic-edge", new Color(0.8078f, 0.6980f, 1.0000f, 1f), new CosmosZoneSoundRecipe(220.00f, 4600.0f, 0.0450f, 0.0600f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Borde de la Vía Láctea", "Edge of the Milky Way", "Linnunradan reuna", 2320, 1920, 1440),
                    new CosmosBilingualText("Salida hacia el Grupo Local", "Toward the Local Group", "Kohti Paikallista ryhmää", 2480, 1880, 1880),
                    new CosmosBilingualText("La galaxia no termina en una línea nítida. Su luz se vuelve tenue y su halo se mezcla con el espacio entre galaxias.", "The galaxy does not end in a sharp line. Its light becomes dim and its halo blends with the space between galaxies.", "Galaksi ei pääty terävään linjaan. Sen valo himmenee ja sen halo sulautuu galaksien väliseen tilaan.", 8040, 9600, 8280)),
                new RawZoneSeed(6, 0, "galactic-edge", new Color(0.9098f, 0.7451f, 1.0000f, 1f), new CosmosZoneSoundRecipe(76.00f, 980.0f, 0.0800f, 0.3300f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Borde galáctico", "galactic edge", "galaktinen reuna", 2040, 1440, 1570),
                    new CosmosBilingualText("Continuidad · salida de la Vía Láctea", "Continuation · leaving the Milky Way", "Jatkoa · poistuminen Linnunradasta", 3280, 3320, 3320),
                    new CosmosBilingualText("Aquí termina exactamente el mapa anterior. La luz del disco se debilita, pero la gravedad de la Vía Láctea todavía alcanza mucho más lejos.", "This is exactly where the previous map ends. The light from the disk is weakening, but the Milky Way's gravity still reaches much further.", "Juuri tähän edellinen kartta päättyy. Levyltä tuleva valo heikkenee, mutta Linnunradan painovoima ulottuu edelleen paljon pidemmälle.", 10320, 10000, 10360)),
                new RawZoneSeed(6, 4, "magellanic-clouds", new Color(1.0000f, 0.4902f, 0.7490f, 1f), new CosmosZoneSoundRecipe(98.00f, 1550.0f, 0.0950f, 0.2500f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Nubes de Magallanes", "Magellanic Clouds", "Magellanin pilvet", 2040, 1450, 1420),
                    new CosmosBilingualText("Dos galaxias irregulares vecinas", "Two neighboring irregular galaxies", "Kaksi epäsäännöllistä naapurigalaksia", 2760, 2840, 2730),
                    new CosmosBilingualText("La Gran y la Pequeña Nube de Magallanes son galaxias satélite visibles desde el hemisferio sur y están interactuando con la Vía Láctea.", "The Large and Small Magellanic Clouds are satellite galaxies visible from the southern hemisphere and are interacting with the Milky Way.", "Suuri ja Pieni Magellanin pilvi ovat eteläiseltä pallonpuoliskolta näkyviä satelliittigalakseja, jotka ovat vuorovaikutuksessa Linnunradan kanssa.", 10600, 9640, 10080)),
                new RawZoneSeed(6, 5, "triangulum", new Color(0.4235f, 0.7882f, 1.0000f, 1f), new CosmosZoneSoundRecipe(121.00f, 2050.0f, 0.0600f, 0.1900f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Galaxia del Triángulo", "Triangulum Galaxy", "Kolmion galaksi", 1960, 1550, 1400),
                    new CosmosBilingualText("M33 · gran espiral del Grupo Local", "M33 · major spiral galaxy in the Local Group", "M33 · Paikallisen ryhmän suuri spiraaligalaksi", 3320, 5120, 4840),
                    new CosmosBilingualText("La galaxia del Triángulo, M33, es la tercera gran espiral del Grupo Local y destaca por sus brillantes regiones de formación estelar.", "The Triangulum Galaxy, M33, is the Local Group's third major spiral galaxy and is notable for its bright star-forming regions.", "Kolmion galaksi M33 on Paikallisen ryhmän kolmas suuri spiraaligalaksi, joka tunnetaan kirkkaista tähtien syntyalueistaan.", 10600, 9960, 10040)),
                new RawZoneSeed(6, 6, "andromeda", new Color(1.0000f, 0.8039f, 0.5373f, 1f), new CosmosZoneSoundRecipe(147.00f, 2850.0f, 0.0500f, 0.1300f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Galaxia de Andrómeda", "Andromeda Galaxy", "Andromedan galaksi", 1800, 1360, 1560),
                    new CosmosBilingualText("M31 · gran destino del recorrido", "M31 · a major destination on the journey", "M31 · matkan suuri pääkohde", 3760, 4520, 2160),
                    new CosmosBilingualText("Andrómeda es la gran galaxia espiral vecina de la Vía Láctea. Su disco inclinado domina visualmente esta parte del viaje.", "Andromeda is the large spiral galaxy neighboring the Milky Way. Its tilted disc visually dominates this part of the trip.", "Andromeda on Linnunradan suuri spiraalinaapuri. Sen kallistunut kiekko hallitsee tämän matkan vaiheen näkymää.", 10520, 9720, 9360)),
                new RawZoneSeed(6, 8, "local-group-limit", new Color(0.7412f, 0.7961f, 1.0000f, 1f), new CosmosZoneSoundRecipe(208.00f, 4300.0f, 0.0350f, 0.0650f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Límite del Grupo Local", "Boundary of the Local Group", "Paikallisen ryhmän raja", 2080, 2440, 1600),
                    new CosmosBilingualText("Frontera gravitatoria · transición a escalas mayores", "Gravitational boundary · transition to larger scales", "Painovoimaraja · siirtyminen suurempiin mittakaavoihin", 4680, 4000, 4640),
                    new CosmosBilingualText("El Grupo Local tampoco tiene una pared nítida. En su límite, la gravedad local cede paso a estructuras mayores y al flujo general de expansión cósmica.", "The Local Group also does not have a clear wall. At its limit, local gravity gives way to larger structures and the general flow of cosmic expansion.", "Paikallisellakaan ryhmällä ei ole selvää seinämää. Sen rajalla paikallinen painovoima antaa tilaa suuremmille rakenteille ja maailmankaikkeuden yleiselle laajenemiselle.", 10800, 10640, 12680)),
                new RawZoneSeed(7, 0, "local-group-limit", new Color(0.7765f, 0.7608f, 1.0000f, 1f), new CosmosZoneSoundRecipe(68.00f, 760.0f, 0.0800f, 0.3400f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Límite del Grupo Local", "Boundary of the Local Group", "Paikallisen ryhmän raja", 2080, 2360, 1660),
                    new CosmosBilingualText("Continuidad · comienza el siguiente salto", "Continuation · the next leap begins", "Jatkoa · seuraava harppaus alkaa", 3320, 3880, 3080),
                    new CosmosBilingualText("El recorrido comienza exactamente donde terminó el anterior: en la frontera dinámica y difusa de nuestra pequeña familia de galaxias.", "The tour begins exactly where the previous one ended: at the dynamic and diffuse border of our small family of galaxies.", "Matka alkaa täsmälleen siitä, mihin edellinen päättyi: pienen galaksiperheemme dynaamiselta ja häilyvältä rajalta.", 9520, 10080, 9600)),
                new RawZoneSeed(7, 4, "virgo-supercluster", new Color(1.0000f, 0.5608f, 0.7373f, 1f), new CosmosZoneSoundRecipe(116.00f, 2180.0f, 0.0600f, 0.1800f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Supercúmulo de Virgo", "Virgo Supercluster", "Neitsyen superjoukko", 1920, 1620, 1710),
                    new CosmosBilingualText("Rama histórica · grupos, cúmulos y filamentos", "Historical grouping · groups, clusters, and filaments", "Historiallinen ryhmittely · ryhmiä, joukkoja ja säikeitä", 4880, 4640, 5680),
                    new CosmosBilingualText("El nombre Supercúmulo de Virgo describe la concentración extensa que incluye nuestro entorno y Virgo; en el mapa dinámico moderno aparece como parte de Laniakea.", "The name Virgo Supercluster describes the extensive concentration that includes our environment and Virgo; on the modern dynamic map it appears as part of Laniakea.", "Nimi Neitsyen superjoukko kuvaa laajaa keskittymää, johon kuuluvat lähiympäristömme ja Neitsyen joukko; nykyisessä dynaamisessa kartassa se on osa Laniakeaa.", 13160, 12480, 12160)),
                new RawZoneSeed(7, 5, "galaxy-flows", new Color(0.4078f, 0.8157f, 1.0000f, 1f), new CosmosZoneSoundRecipe(132.00f, 2600.0f, 0.0520f, 0.1450f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Corrientes galácticas", "Galactic flows", "Galaksien virtaukset", 2000, 1590, 1710),
                    new CosmosBilingualText("Velocidades peculiares · rutas gravitatorias", "Peculiar velocities · gravitational routes", "Poikkeusnopeudet · painovoiman määräämät reitit", 3960, 4200, 4120),
                    new CosmosBilingualText("Al retirar matemáticamente la expansión general, los movimientos residuales dibujan corrientes que siguen valles del campo gravitatorio.", "After the overall expansion is mathematically removed, the remaining motions trace flows that follow valleys in the gravitational field.", "Kun yleinen laajeneminen poistetaan laskennallisesti, jäljelle jäävät liikkeet piirtävät virtauksia, jotka seuraavat painovoimakentän laaksoja.", 10280, 10520, 9920)),
                new RawZoneSeed(7, 7, "great-attractor", new Color(1.0000f, 0.6667f, 0.4235f, 1f), new CosmosZoneSoundRecipe(72.00f, 1180.0f, 0.0900f, 0.5800f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Gran Atractor", "Great Attractor", "Suuri attraktori", 1680, 1540, 1890),
                    new CosmosBilingualText("Convergencia gravitatoria · no es un objeto único", "Gravitational convergence · not a single object", "Painovoiman aiheuttama lähentyminen · ei yksittäinen kohde", 2360, 4520, 5120),
                    new CosmosBilingualText("El Gran Atractor es una región de convergencia del campo de velocidades, asociada a grandes concentraciones de masa; no es una esfera, estrella ni agujero negro gigantesco.", "The Great Attractor is a region where the velocity field converges, associated with large concentrations of mass; it is not one gigantic sphere, star, or black hole.", "Suuri attraktori on nopeuskentän lähentymisalue, joka liittyy suuriin massakeskittymiin; se ei ole yksi jättimäinen pallo, tähti tai musta aukko.", 12960, 14080, 11920)),
                new RawZoneSeed(7, 8, "laniakea-basin", new Color(0.8039f, 0.5725f, 1.0000f, 1f), new CosmosZoneSoundRecipe(154.00f, 3280.0f, 0.0400f, 0.1100f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Cuenca de Laniakea", "Laniakea Basin", "Laniakea-allas", 2040, 1680, 1750),
                    new CosmosBilingualText("Cuenca de flujo · cientos de millones de años luz", "Flow basin · hundreds of millions of light-years", "Virtausallas · satoja miljoonia valovuosia", 4520, 4560, 4400),
                    new CosmosBilingualText("Laniakea se define como una inmensa cuenca de velocidades peculiares: dentro de su frontera, las líneas reconstruidas pertenecen al mismo sistema de drenaje cósmico.", "Laniakea is defined as an immense basin of peculiar velocities: within its boundary, the reconstructed streamlines belong to the same cosmic drainage basin.", "Laniakea määritellään valtavaksi poikkeusnopeuksien altaaksi: sen rajan sisällä rekonstruoidut virtausviivat kuuluvat samaan kosmiseen valuma-alueeseen.", 13360, 11080, 11880)),
                new RawZoneSeed(7, 9, "laniakea-limit", new Color(0.7451f, 0.8353f, 1.0000f, 1f), new CosmosZoneSoundRecipe(202.00f, 4400.0f, 0.0320f, 0.0650f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Límite de Laniakea", "Laniakea Boundary", "Laniakean raja", 2040, 1840, 1440),
                    new CosmosBilingualText("Divisoria dinámica · entrada a la red cósmica", "Dynamic divide · entrance to the cosmic web", "Dynaaminen vedenjakaja · sisäänkäynti kosmiseen verkkoon", 4320, 4320, 5760),
                    new CosmosBilingualText("En el borde de la cuenca, las líneas de flujo se separan hacia otras grandes regiones. La vista está lista para revelar la red cósmica completa.", "At the basin's edge, streamlines diverge toward other large regions. The view is ready to reveal the full cosmic web.", "Altaan reunalla virtausviivat erkanevat kohti muita suuria alueita. Näkymä on valmis paljastamaan koko kosmisen verkon.", 11080, 9320, 9360)),
                new RawZoneSeed(8, 0, "laniakea-limit", new Color(0.7608f, 0.8039f, 1.0000f, 1f), new CosmosZoneSoundRecipe(72.00f, 850.0f, 0.0700f, 0.3100f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Límite de Laniakea", "Laniakea Boundary", "Laniakean raja", 2040, 1800, 1390),
                    new CosmosBilingualText("Continuidad · una cuenca dentro de una red mayor", "Continuation · a basin within a larger web", "Jatkoa · allas laajemman verkon sisällä", 4240, 4320, 3560),
                    new CosmosBilingualText("El recorrido comienza exactamente donde terminó el anterior: la divisoria dinámica de Laniakea se desvanece dentro de una estructura mucho más extensa.", "The tour begins exactly where the previous one ended: the dynamic divide of Laniakea fades into a much larger structure.", "Matka alkaa täsmälleen siitä, mihin edellinen päättyi: Laniakean dynaaminen vedenjakaja häviää paljon laajempaan rakenteeseen.", 11680, 9840, 10200)),
                new RawZoneSeed(8, 2, "galactic-filaments", new Color(0.3373f, 0.8235f, 1.0000f, 1f), new CosmosZoneSoundRecipe(91.00f, 1280.0f, 0.0600f, 0.2300f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Filamentos galácticos", "Galaxy filaments", "Galaksisäikeet", 2120, 1390, 1210),
                    new CosmosBilingualText("Hebras de materia · galaxias alineadas por la estructura", "Strands of matter · galaxies aligned by the structure", "Aineen säikeitä · rakenteen suuntaisia galakseja", 4880, 5000, 5000),
                    new CosmosBilingualText("Los filamentos son regiones alargadas donde se concentran materia oscura, gas y galaxias. Sus luces dibujan la columna vertebral de la red cósmica.", "Filaments are elongated regions where dark matter, gas, and galaxies concentrate. The galaxies' light traces the backbone of the cosmic web.", "Säikeet ovat pitkänomaisia alueita, joihin pimeä aine, kaasu ja galaksit keskittyvät. Galaksien valo piirtää kosmisen verkon rungon.", 12240, 12360, 11400)),
                new RawZoneSeed(8, 3, "cluster-nodes", new Color(1.0000f, 0.7804f, 0.5020f, 1f), new CosmosZoneSoundRecipe(106.00f, 1760.0f, 0.0650f, 0.1800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Nodos de cúmulos", "Cluster nodes", "Galaksijoukkojen solmut", 1800, 890, 2060),
                    new CosmosBilingualText("Intersecciones densas · cúmulos y materia caliente", "Dense intersections · galaxy clusters and hot matter", "Tiheitä risteyksiä · galaksijoukkoja ja kuumaa ainetta", 4960, 5120, 5440),
                    new CosmosBilingualText("Donde varios filamentos se encuentran, la densidad aumenta y nacen nodos dominados por grandes cúmulos de galaxias.", "Where several filaments meet, the density increases and nodes dominated by large galaxy clusters are born.", "Kun useat säikeet kohtaavat, tiheys kasvaa ja suurten galaksijoukkojen hallitsemia solmuja syntyy.", 9200, 9240, 7920)),
                new RawZoneSeed(8, 5, "galactic-walls", new Color(0.7333f, 0.5373f, 1.0000f, 1f), new CosmosZoneSoundRecipe(98.00f, 1480.0f, 0.0550f, 0.2100f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Paredes galácticas", "galactic walls", "Galaksiseinämät", 2040, 1420, 1310),
                    new CosmosBilingualText("Láminas inmensas · superficies entre vacíos", "Immense sheets · surfaces between voids", "Valtavia seinämiä · pintoja tyhjiöiden välissä", 4440, 4080, 4720),
                    new CosmosBilingualText("Las paredes son distribuciones anchas y relativamente delgadas de galaxias que separan grandes vacíos y alimentan filamentos más densos.", "Walls are wide, relatively thin distributions of galaxies that separate large voids and feed denser filaments.", "Seinämät ovat leveitä ja suhteellisen ohuita galaksijakaumia, jotka erottavat suuria tyhjiöitä ja syöttävät tiheämpiä säikeitä.", 10160, 9120, 10520)),
                new RawZoneSeed(8, 10, "large-scale-structure", new Color(0.8431f, 0.8863f, 1.0000f, 1f), new CosmosZoneSoundRecipe(198.00f, 4680.0f, 0.0250f, 0.0550f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Estructura a gran escala", "Large-scale structure", "Suuren mittakaavan rakenne", 2440, 1920, 1880),
                    new CosmosBilingualText("Destino visual · la red cósmica como sistema completo", "Visual destination · the cosmic web as a complete system", "Visuaalinen määränpää · kosminen verkko kokonaisuutena", 4760, 5240, 4400),
                    new CosmosBilingualText("La vista culmina en una red tridimensional de filamentos, paredes, nodos y vacíos. Laniakea es apenas una cuenca reconocible dentro de este patrón.", "The view culminates in a three-dimensional network of filaments, walls, nodes and voids. Laniakea is just one recognizable basin within this pattern.", "Näkymä huipentuu säikeiden, seinämien, solmujen ja tyhjiöiden kolmiulotteiseen verkkoon. Laniakea on vain yksi tämän kuvion tunnistettavista altaista.", 12400, 13760, 11440)),
                new RawZoneSeed(9, 0, "large-scale-structure", new Color(0.4941f, 0.8000f, 1.0000f, 1f), new CosmosZoneSoundRecipe(66.00f, 820.0f, 0.0550f, 0.3000f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Estructura a gran escala", "Large-scale structure", "Suuren mittakaavan rakenne", 2440, 1830, 1950),
                    new CosmosBilingualText("Continuidad · la red cósmica en el universo cercano", "Continuation · the cosmic web in the nearby universe", "Jatkoa · kosminen verkko lähimaailmankaikkeudessa", 4120, 4640, 4520),
                    new CosmosBilingualText("El recorrido comienza exactamente donde terminó el anterior: filamentos, nodos, paredes y vacíos aún forman una red tridimensional reconocible.", "The journey begins exactly where the previous one ended: filaments, nodes, walls, and voids still form a recognizable three-dimensional web.", "Matka alkaa täsmälleen siitä, mihin edellinen päättyi: säikeet, solmut, seinämät ja tyhjiöt muodostavat yhä tunnistettavan kolmiulotteisen verkon.", 12680, 12120, 11680)),
                new RawZoneSeed(9, 4, "quasar-era", new Color(1.0000f, 0.5922f, 0.4039f, 1f), new CosmosZoneSoundRecipe(116.00f, 2380.0f, 0.0450f, 0.1400f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Era de los cuásares", "Quasar era", "Kvasaarien aikakausi", 1800, 1190, 1740),
                    new CosmosBilingualText("Núcleos activos · un universo joven y extremadamente energético", "Active nuclei · a young and extremely energetic universe", "Aktiivisia ytimiä · nuori ja erittäin energinen maailmankaikkeus", 6480, 5640, 5520),
                    new CosmosBilingualText("En una época temprana abundaron los cuásares: núcleos galácticos alimentados por agujeros negros supermasivos, visibles a través de distancias inmensas.", "In an early era, quasars abounded: galactic nuclei powered by supermassive black holes, visible over immense distances.", "Varhaisessa maailmankaikkeudessa kvasaareja oli runsaasti: ne ovat supermassiivisten mustien aukkojen energisoimia galaksiytimiä, jotka näkyvät valtavien etäisyyksien päähän.", 11560, 10280, 12280)),
                new RawZoneSeed(9, 5, "first-galaxies", new Color(1.0000f, 0.7725f, 0.4902f, 1f), new CosmosZoneSoundRecipe(132.00f, 2860.0f, 0.0520f, 0.1200f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Primeras galaxias", "First galaxies", "Ensimmäiset galaksit", 1680, 1270, 1320),
                    new CosmosBilingualText("Amanecer cósmico · nacen estrellas, cúmulos y galaxias", "Cosmic dawn · stars, clusters and galaxies are born", "Kosminen aamunkoitto · tähdet, tähtijoukot ja galaksit syntyvät", 6200, 5040, 7760),
                    new CosmosBilingualText("Las primeras galaxias surgieron de pequeñas concentraciones de materia. Eran menos maduras y ayudaron a transformar el gas neutro del universo temprano.", "The first galaxies arose from small concentrations of matter. They were less mature and helped transform the neutral gas of the early universe.", "Ensimmäiset galaksit syntyivät pienistä ainekeskittymistä. Ne olivat vähemmän kehittyneitä ja auttoivat muuttamaan varhaisen maailmankaikkeuden neutraalia kaasua.", 11600, 10600, 11600)),
                new RawZoneSeed(9, 8, "cmb", new Color(1.0000f, 0.8039f, 0.5569f, 1f), new CosmosZoneSoundRecipe(74.00f, 910.0f, 0.1900f, 0.3100f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Fondo cósmico de microondas", "Cosmic Microwave Background", "Kosminen mikroaaltotausta", 2880, 2100, 1970),
                    new CosmosBilingualText("Luz fósil · universo de unos 380 000 años", "Fossil light · universe about 380,000 years old", "Fossiilista valoa · maailmankaikkeus noin 380 000 vuoden ikäisenä", 5240, 6200, 7960),
                    new CosmosBilingualText("El fondo cósmico de microondas es la luz electromagnética más antigua que podemos observar directamente. Sus pequeñas variaciones conservan las semillas de la estructura posterior.", "The cosmic microwave background is the oldest electromagnetic light that we can directly observe. Its small variations preserve the seeds of the later structure.", "Kosminen mikroaaltotausta on vanhinta suoraan havaittavaa sähkömagneettista valoa. Sen pienet vaihtelut säilyttävät myöhempien rakenteiden siemenet.", 13880, 11400, 12400)),
                new RawZoneSeed(9, 10, "observable-limit", new Color(1.0000f, 0.9412f, 0.8549f, 1f), new CosmosZoneSoundRecipe(96.00f, 1500.0f, 0.1400f, 0.2500f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Límite del universo observable", "Boundary of the observable universe", "Havaittavan maailmankaikkeuden raja", 2680, 3080, 2580),
                    new CosmosBilingualText("Borde de nuestro mapa · no borde físico del universo", "Edge of our map · not a physical edge of the universe", "Karttamme reuna · ei maailmankaikkeuden fyysinen reuna", 4880, 4480, 4280),
                    new CosmosBilingualText("Aquí termina lo que puede mostrarnos la luz disponible. El universo completo puede extenderse mucho más, pero esa región no es actualmente visible de forma directa.", "Here ends what the available light can show us. The entire universe may extend much further, but that region is not currently directly visible.", "Tähän päättyy se, mitä meille saapunut valo voi näyttää. Koko maailmankaikkeus voi ulottua paljon kauemmas, mutta sitä aluetta ei tällä hetkellä voi nähdä suoraan.", 11440, 12080, 12520)),
                new RawZoneSeed(10, 0, "cmb", new Color(1.0000f, 0.7961f, 0.5804f, 1f), new CosmosZoneSoundRecipe(62.00f, 760.0f, 0.0850f, 0.3100f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Fondo cósmico de microondas", "Cosmic Microwave Background", "Kosminen mikroaaltotausta", 2880, 2180, 2580),
                    new CosmosBilingualText("≈ 380 000 años · la luz electromagnética más antigua observable", "≈ 380,000 years · oldest observable electromagnetic light", "≈ 380 000 vuotta · vanhin havaittava sähkömagneettinen valo", 6720, 6640, 8840),
                    new CosmosBilingualText("El recorrido comienza exactamente en la superficie de última dispersión: un mapa de pequeñas variaciones de temperatura, no una pared ni el borde del universo.", "The journey begins exactly at the surface of last scattering: a map of small temperature variations, not a wall or the edge of the universe.", "Matka alkaa täsmälleen viimeisen sironnan pinnalta: se on pienten lämpötilavaihteluiden kartta, ei seinämä tai maailmankaikkeuden reuna.", 11520, 11000, 11360)),
                new RawZoneSeed(10, 3, "nucleosynthesis", new Color(1.0000f, 0.8275f, 0.3686f, 1f), new CosmosZoneSoundRecipe(98.00f, 1720.0f, 0.0850f, 0.1800f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Nucleosíntesis primordial", "Big Bang nucleosynthesis", "Alkuräjähdyksen nukleosynteesi", 2600, 2250, 3150),
                    new CosmosBilingualText("Primeros minutos · se forman núcleos ligeros", "First minutes · light nuclei form", "Ensimmäiset minuutit · muodostuu kevyitä ytimiä", 4200, 3280, 4240),
                    new CosmosBilingualText("Al enfriarse el universo, protones y neutrones pudieron unirse brevemente para producir deuterio, helio y pequeñas cantidades de litio.", "As the universe cooled, protons and neutrons were able to join together briefly to produce deuterium, helium, and small amounts of lithium.", "Maailmankaikkeuden jäähtyessä protonit ja neutronit saattoivat liittyä hetkeksi yhteen ja muodostaa deuteriumia, heliumia sekä pieniä määriä litiumia.", 11160, 11720, 11880)),
                new RawZoneSeed(10, 4, "lepton-era", new Color(0.4980f, 0.8824f, 1.0000f, 1f), new CosmosZoneSoundRecipe(118.00f, 2300.0f, 0.1200f, 0.1400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Era de los leptones", "Lepton Epoch", "Leptonikausi", 1880, 1340, 1120),
                    new CosmosBilingualText("Alrededor del primer segundo · leptones, fotones y neutrinos", "Around the first second · leptons, photons, and neutrinos", "Ensimmäisen sekunnin tienoilla · leptoneja, fotoneja ja neutriinoja", 5880, 5600, 5760),
                    new CosmosBilingualText("A temperaturas aún mayores, electrones, positrones, neutrinos y fotones dominaban gran parte del contenido energético mientras el universo seguía expandiéndose y enfriándose.", "At even higher temperatures, electrons, positrons, neutrinos and photons dominated much of the energy content as the universe continued to expand and cool.", "Vielä korkeammissa lämpötiloissa elektronit, positronit, neutriinot ja fotonit muodostivat suuren osan energiasisällöstä maailmankaikkeuden laajentuessa ja jäähtyessä.", 14280, 12440, 13480)),
                new RawZoneSeed(10, 7, "inflation", new Color(0.6824f, 0.5059f, 1.0000f, 1f), new CosmosZoneSoundRecipe(205.00f, 4800.0f, 0.0800f, 0.0450f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Universo inflacionario", "Inflationary universe", "Inflaatiomaailmankaikkeus", 2000, 2230, 2520),
                    new CosmosBilingualText("Época propuesta · expansión acelerada extremadamente temprana", "Proposed epoch · extremely early accelerated expansion", "Ehdotettu aikakausi · äärimmäisen varhainen kiihtyvä laajeneminen", 5680, 5240, 5680),
                    new CosmosBilingualText("La inflación es un escenario en el que el propio espacio se expandió de forma extraordinariamente rápida. No fue una explosión dentro de un vacío ni tuvo un centro espacial.", "Inflation is a scenario in which space itself expanded extraordinarily quickly. It was not an explosion within a vacuum nor did it have a spatial center.", "Inflaatio on skenaario, jossa itse avaruus laajeni poikkeuksellisen nopeasti. Se ei ollut räjähdys tyhjiössä, eikä sillä ollut avaruudellista keskustaa.", 13240, 13200, 12280)),
                new RawZoneSeed(10, 8, "known-physics-limit", new Color(0.9255f, 0.8627f, 1.0000f, 1f), new CosmosZoneSoundRecipe(238.00f, 5400.0f, 0.2100f, 0.0250f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Límite de la física conocida", "Boundary of known physics", "Tunnetun fysiikan raja", 2720, 2360, 1670),
                    new CosmosBilingualText("Frontera teórica · gravedad cuántica y condiciones iniciales", "Theoretical frontier · quantum gravity and initial conditions", "Teoreettinen raja · kvanttipainovoima ja alkuehdot", 5400, 5000, 4960),
                    new CosmosBilingualText("La imagen se disuelve porque nuestras teorías actuales dejan de ser suficientes. Esta región no muestra una singularidad observada, un primer punto confirmado ni algo conocido antes de la inflación.", "The image dissolves because our current theories are no longer sufficient. This region does not show an observed singularity, a confirmed first point, or something known before inflation.", "Kuva häviää, koska nykyiset teoriamme eivät enää riitä. Alue ei esitä havaittua singulariteettia, vahvistettua ensimmäistä hetkeä eikä mitään tunnettua inflaatiota edeltävää vaihetta.", 14800, 13680, 15160)),
                new RawZoneSeed(11, 0, "handoff", new Color(0.8706f, 0.9020f, 1.0000f, 1f), new CosmosZoneSoundRecipe(78.00f, 920.0f, 0.0800f, 0.2200f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Límite de la física conocida", "Boundary of known physics", "Tunnetun fysiikan raja", 2720, 2720, 1580),
                    new CosmosBilingualText("Continuidad con el universo temprano · la cronología se detiene", "Continuation from the early universe · the timeline stops", "Jatkoa varhaisesta maailmankaikkeudesta · aikajana pysähtyy", 4960, 4400, 5000),
                    new CosmosBilingualText("Aquí termina el recorrido hacia épocas cada vez más tempranas. Desde este punto, subir ya no significa retroceder en el tiempo: significa alejarse gradualmente de la evidencia directa.", "Here ends the journey towards ever earlier times. From this point, going up no longer means going back in time: it means gradually moving away from direct evidence.", "Tähän päättyy matka kohti yhä varhaisempia aikakausia. Tästä eteenpäin ylöspäin siirtyminen ei enää tarkoita ajassa taaksepäin kulkemista, vaan asteittaista etääntymistä suorasta todistusaineistosta.", 13560, 14120, 16760)),
                new RawZoneSeed(11, 4, "testable-predictions", new Color(1.0000f, 0.6471f, 0.4235f, 1f), new CosmosZoneSoundRecipe(154.00f, 3100.0f, 0.0950f, 0.0850f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Predicciones comprobables", "Testable predictions", "Testattavissa olevat ennusteet", 2200, 1630, 2130),
                    new CosmosBilingualText("Una teoría se arriesga · la observación puede apoyarla o descartarla", "A theory makes testable commitments · observation can support or rule it out", "Teoria tekee testattavia ennusteita · havainto voi tukea tai kumota sen", 5600, 6080, 6400),
                    new CosmosBilingualText("Una predicción científica valiosa especifica qué patrón debería aparecer, dónde buscarlo y qué resultado contaría en contra del modelo.", "A valuable scientific prediction specifies what pattern should appear, where to look for it, and what result would count against the model.", "Hyvä tieteellinen ennuste kertoo, millaisen kuvion pitäisi näkyä, mistä sitä etsitään ja mikä tulos puhuisi mallia vastaan.", 10600, 10120, 9440)),
                new RawZoneSeed(11, 6, "quantum-gravity", new Color(0.6902f, 0.5059f, 1.0000f, 1f), new CosmosZoneSoundRecipe(198.00f, 4250.0f, 0.1250f, 0.0450f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Modelos de gravedad cuántica", "Quantum gravity models", "Kvanttigravitaation mallit", 2600, 1760, 2010),
                    new CosmosBilingualText("Sin teoría confirmada · geometrías alternativas · pruebas difíciles", "No confirmed theory · alternative geometries · difficult tests", "Ei vahvistettua teoriaa · vaihtoehtoiset geometriat · vaikeat testit", 6720, 6120, 5880),
                    new CosmosBilingualText("Aquí las representaciones se vuelven abstractas porque todavía no existe una teoría de gravedad cuántica verificada de forma experimental.", "Here the representations become abstract because there is still no experimentally verified theory of quantum gravity.", "Tässä esityksistä tulee abstrakteja, koska kvanttigravitaatiosta ei vielä ole kokeellisesti vahvistettua teoriaa.", 9400, 8560, 7600)),
                new RawZoneSeed(11, 8, "knowable-frontier", new Color(0.9333f, 0.9373f, 1.0000f, 1f), new CosmosZoneSoundRecipe(242.00f, 5300.0f, 0.2100f, 0.0220f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Frontera de lo conocible", "Frontier of the knowable", "Tietämisen raja", 2120, 2240, 1150),
                    new CosmosBilingualText("Límites de medición · horizontes causales · teorías incompletas", "Measurement limits · causal horizons · incomplete theories", "Mittausten rajat · kausaalihorisontit · epätäydelliset teoriat", 5920, 5240, 6440),
                    new CosmosBilingualText("La ausencia de una respuesta confirmada no es un vacío de la ciencia. Es la frontera donde comienzan nuevas preguntas.", "The absence of a confirmed answer is not a gap in science. It is the border where new questions begin.", "Vahvistetun vastauksen puuttuminen ei ole aukko tieteessä. Se on raja, josta alkavat uudet kysymykset.", 8680, 9160, 8320)),
                new RawZoneSeed(12, 0, "handoff", new Color(0.8275f, 0.8706f, 1.0000f, 1f), new CosmosZoneSoundRecipe(76.00f, 820.0f, 0.0450f, 0.1200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Frontera de lo conocible", "Frontier of the knowable", "Tietämisen raja", 2120, 2040, 1210),
                    new CosmosBilingualText("La ruta del origen termina · el eje temporal vuelve a empezar", "The journey toward the origin ends · the time axis begins again", "Matka kohti alkuperää päättyy · aika-akseli alkaa uudelleen", 5280, 6040, 5400),
                    new CosmosBilingualText("La aplicación anterior terminó separando evidencia y desconocimiento. Aquí giramos la mirada: regresamos al universo observable de hoy y proyectamos su evolución hacia delante.", "The previous app ended by separating evidence from the unknown. Here we turn our gaze back to today's observable universe and project its evolution forward.", "Edellinen sovellus päättyi erottamalla todistusaineiston tuntemattomasta. Tässä käännämme katseen takaisin nykyiseen havaittavaan maailmankaikkeuteen ja arvioimme sen tulevaa kehitystä.", 12280, 11800, 13680)),
                new RawZoneSeed(12, 5, "degenerate", new Color(0.7020f, 0.8039f, 0.9098f, 1f), new CosmosZoneSoundRecipe(54.00f, 610.0f, 0.0350f, 0.0400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Era degenerada", "Degenerate Era", "Degeneraation aikakausi", 1880, 1590, 2170),
                    new CosmosBilingualText("Después de ~10¹⁴ años · remanentes fríos y física no confirmada", "After ~10¹⁴ years · cold remnants and unconfirmed physics", "Noin 10¹⁴ vuoden jälkeen · kylmiä jäännöksiä ja vahvistamatonta fysiikkaa", 8080, 6480, 7880),
                    new CosmosBilingualText("Predominan enanas blancas, estrellas de neutrones, enanas marrones, planetas congelados y objetos compactos. Su destino depende de procesos extremadamente lentos y de si partículas como el protón son realmente estables.", "White dwarfs, neutron stars, brown dwarfs, frozen planets and compact objects predominate. Their fate depends on extremely slow processes and whether particles like the proton are truly stable.", "Valkoiset kääpiöt, neutronitähdet, ruskeat kääpiöt, jäätyneet planeetat ja tiiviit kappaleet hallitsevat. Niiden kohtalo riippuu äärimmäisen hitaista prosesseista ja siitä, ovatko protonin kaltaiset hiukkaset todella vakaita.", 17720, 15320, 18360)),
                new RawZoneSeed(12, 6, "black-holes", new Color(0.5569f, 0.4941f, 0.8863f, 1f), new CosmosZoneSoundRecipe(44.00f, 430.0f, 0.0700f, 0.0280f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Era de los agujeros negros", "Black Hole Era", "Mustien aukkojen aikakausi", 2440, 1730, 1960),
                    new CosmosBilingualText("Remoto · los agujeros negros concentran la energía restante", "Distant future · black holes concentrate the remaining energy", "Kaukainen tulevaisuus · mustat aukot keskittävät jäljellä olevan energian", 5360, 5160, 6120),
                    new CosmosBilingualText("Cuando casi toda luz estelar ha terminado, los agujeros negros pueden ser algunos de los últimos depósitos compactos de masa y energía. Aun así, no deben representarse como eternos.", "When almost all starlight has ended, black holes may be some of the last compact reservoirs of mass and energy. Even so, they should not be represented as eternal.", "Kun melkein kaikki tähtien valo on loppunut, mustat aukot voivat olla viimeisiä kompakteja massan ja energian varastoja. Silti niitä ei pitäisi esittää ikuisina.", 13320, 12400, 12360)),
                new RawZoneSeed(12, 9, "unknown", new Color(0.8941f, 0.9059f, 1.0000f, 1f), new CosmosZoneSoundRecipe(30.00f, 240.0f, 0.1150f, 0.0140f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Futuro indeterminado", "Undetermined future", "Määrittelemätön tulevaisuus", 2360, 1500, 1960),
                    new CosmosBilingualText("Sin una cronología única · las posibilidades se ramifican", "No single timeline · the possibilities branch", "Ei yhtä aikajanaa · mahdollisuudet haarautuvat", 5400, 4520, 3760),
                    new CosmosBilingualText("El futuro más lejano no es una película ya conocida. Es una extrapolación basada en las leyes que podemos medir hoy, y esas leyes pueden resultar incompletas en escalas nunca observadas.", "The far future is not a movie whose ending is already known. It is an extrapolation based on the laws we can measure today, and those laws may prove incomplete on scales never observed.", "Kaukaisin tulevaisuus ei ole elokuva, jonka loppu tunnetaan jo. Se on nykyään mitattaviin lakeihin perustuva ekstrapolaatio, ja nämä lait voivat osoittautua puutteellisiksi mittakaavoissa, joita ei ole koskaan havaittu.", 13600, 14760, 16400)),
                new RawZoneSeed(13, 0, "residual", new Color(0.6588f, 0.7961f, 1.0000f, 1f), new CosmosZoneSoundRecipe(42.00f, 420.0f, 0.0800f, 0.0180f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Universo residual", "Residual universe", "Jäännösmaailmankaikkeus", 1920, 1840, 1790),
                    new CosmosBilingualText("Después de las estrellas · campos y partículas extremadamente dispersos", "After stars · extremely dispersed fields and particles", "Tähtien jälkeen · erittäin hajallaan olevat kentät ja hiukkaset", 6560, 5200, 5320),
                    new CosmosBilingualText("El recorrido comienza donde terminó el futuro cósmico. Incluso un universo casi vacío todavía puede contener radiación, neutrinos, partículas estables y campos cuánticos.", "The journey begins where the cosmic future ended. Even a nearly empty universe can still contain radiation, neutrinos, stable particles, and quantum fields.", "Matka alkaa siitä, mihin kosminen tulevaisuus päättyi. Lähes tyhjässäkin maailmankaikkeudessa voi olla säteilyä, neutriinoja, vakaita hiukkasia ja kvanttikenttiä.", 14400, 12640, 13560)),
                new RawZoneSeed(13, 2, "molecules", new Color(0.3765f, 0.9216f, 0.7451f, 1f), new CosmosZoneSoundRecipe(72.00f, 980.0f, 0.0350f, 0.0800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Moléculas", "Molecules", "Molekyylit", 930, 860, 940),
                    new CosmosBilingualText("≈10⁻¹⁰–10⁻⁸ m · átomos unidos por enlaces", "≈10⁻¹⁰–10⁻⁸ m · atoms joined by bonds", "Noin 10⁻¹⁰–10⁻⁸ m · sidoksilla liittyneitä atomeja", 8640, 6920, 7080),
                    new CosmosBilingualText("Una molécula es una estructura de átomos enlazados. Su forma, distribución electrónica y movimiento determinan muchas propiedades químicas.", "A molecule is a structure of bonded atoms. Its shape, electron distribution, and motion determine many chemical properties.", "Molekyyli on toisiinsa sitoutuneiden atomien rakenne. Sen muoto, elektronijakauma ja liike määräävät monia kemiallisia ominaisuuksia.", 10240, 9400, 12720)),
                new RawZoneSeed(13, 3, "atoms", new Color(0.4353f, 0.7569f, 1.0000f, 1f), new CosmosZoneSoundRecipe(86.00f, 1320.0f, 0.0280f, 0.1200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Átomos", "Atoms", "Atomit", 880, 640, 580),
                    new CosmosBilingualText("≈10⁻¹⁰ m · núcleo compacto y nube electrónica", "≈10⁻¹⁰ m · compact nucleus and electron cloud", "Noin 10⁻¹⁰ m · tiivis ydin ja elektronipilvi", 5960, 5120, 6320),
                    new CosmosBilingualText("Los electrones no recorren órbitas planetarias fijas. Se describen mediante estados cuánticos y distribuciones de probabilidad alrededor del núcleo.", "Electrons do not travel in fixed planetary orbits. They are described by quantum states and probability distributions around the nucleus.", "Elektronit eivät kulje kiinteillä planeettaradoilla. Niitä kuvaavat kvanttitilat ja todennäköisyysjakaumat ytimen ympärillä.", 11160, 9160, 9560)),
                new RawZoneSeed(13, 4, "nucleus", new Color(1.0000f, 0.7373f, 0.4039f, 1f), new CosmosZoneSoundRecipe(98.00f, 1540.0f, 0.0450f, 0.1500f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Núcleo atómico", "atomic nucleus", "atomiydin", 1640, 1640, 1100),
                    new CosmosBilingualText("≈10⁻¹⁵–10⁻¹⁴ m · protones, neutrones y energía de enlace", "≈10⁻¹⁵–10⁻¹⁴ m · protons, neutrons and binding energy", "≈10⁻¹⁵–10⁻¹⁴ m · protonit, neutronit ja sitoutumisenergia", 8960, 8800, 5560),
                    new CosmosBilingualText("El núcleo contiene protones y neutrones ligados por la interacción fuerte residual. Sus configuraciones explican isótopos, radiactividad, fusión y fisión.", "The nucleus contains protons and neutrons bound by the residual strong interaction. Their configurations explain isotopes, radioactivity, fusion and fission.", "Ytimessä on protoneja ja neutroneja, joita sitoo vahvan vuorovaikutuksen jäännösvoima. Niiden rakenteet selittävät isotoopit, radioaktiivisuuden, fuusion ja fission.", 13000, 12240, 13720)),
                new RawZoneSeed(13, 6, "quarks", new Color(0.9490f, 0.3882f, 0.7961f, 1f), new CosmosZoneSoundRecipe(126.00f, 1980.0f, 0.0700f, 0.2200f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Quarks y gluones", "Quarks and gluons", "Kvarkit ja gluonit", 1840, 1620, 1550),
                    new CosmosBilingualText("Sin tamaño interno observado · confinamiento y carga de color", "No observed internal size · confinement and color charge", "Ei havaittua sisäistä kokoa · sulkeutuminen ja värivaraus", 5680, 6000, 5040),
                    new CosmosBilingualText("Los quarks up y down forman protones y neutrones. Los gluones transmiten la interacción fuerte. La carga de color es una propiedad cuántica, no un color visible.", "Up and down quarks form protons and neutrons. Gluons carry the strong interaction. Color charge is a quantum property, not a visible color.", "Ylös- ja alas-kvarkit muodostavat protoneja ja neutroneja. Gluonit välittävät vahvan vuorovaikutuksen. Värivaraus on kvanttiominaisuus, ei näkyvä väri.", 12600, 11920, 13600)),
                new RawZoneSeed(13, 9, "fields", new Color(0.3608f, 0.8471f, 1.0000f, 1f), new CosmosZoneSoundRecipe(158.00f, 2720.0f, 0.0350f, 0.3100f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Campos cuánticos", "Quantum fields", "Kvanttikentät", 1560, 1570, 1190),
                    new CosmosBilingualText("Campos en todo el espacio · excitaciones localizadas e interacciones", "Fields throughout space · localized excitations and interactions", "Kentät koko avaruudessa · paikalliset herätteet ja vuorovaikutukset", 6160, 5920, 6240),
                    new CosmosBilingualText("En teoría cuántica de campos, una partícula puede entenderse como una excitación cuantizada de un campo. Los campos, no pequeñas bolas permanentes, son el lenguaje fundamental del Modelo Estándar.", "In quantum field theory, a particle can be understood as a quantized excitation of a field. Fields, not permanent little balls, are the fundamental language of the Standard Model.", "Kvanttikenttäteoriassa hiukkanen voidaan ymmärtää kentän kvantittuneeksi viritystilaksi. Standardimallin peruskielen muodostavat kentät, eivät pysyvät pienet pallot.", 14240, 14520, 12600)),
                new RawZoneSeed(13, 10, "vacuum", new Color(0.6784f, 0.8863f, 1.0000f, 1f), new CosmosZoneSoundRecipe(166.00f, 3100.0f, 0.0250f, 0.3400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Vacío cuántico", "Quantum vacuum", "Kvanttityhjiö", 1840, 1290, 1250),
                    new CosmosBilingualText("No es ‘nada’ · estado base de campos cuánticos", "It is not 'nothing' · ground state of quantum fields", "Ei ”ei mitään” · kvanttikenttien perustila", 4360, 4240, 3760),
                    new CosmosBilingualText("El vacío cuántico es el estado de menor energía de los campos, no una caja llena de partículas ordinarias ocultas. Sus propiedades pueden producir efectos físicos medibles.", "The quantum vacuum is the lowest energy state of the fields, not a box full of hidden ordinary particles. Its properties can produce measurable physical effects.", "Kvanttityhjiö on kenttien alhaisin energiatila, ei laatikko täynnä piilotettuja tavallisia hiukkasia. Sen ominaisuudet voivat tuottaa mitattavissa olevia fysikaalisia vaikutuksia.", 12360, 12800, 14520)),
                new RawZoneSeed(13, 11, "frontier", new Color(0.9333f, 0.9451f, 1.0000f, 1f), new CosmosZoneSoundRecipe(176.00f, 3400.0f, 0.0500f, 0.3800f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Frontera de la física fundamental", "Frontier of fundamental physics", "Perusfysiikan raja", 2920, 2400, 1610),
                    new CosmosBilingualText("Lo descrito termina · las preguntas fundamentales permanecen", "The current description ends · fundamental questions remain", "Nykyinen kuvaus päättyy · perustavat kysymykset jäävät", 5080, 4840, 4600),
                    new CosmosBilingualText("Hemos descrito gran parte de lo que existe, pero todavía no sabemos por qué estas partículas, estos campos y estas leyes forman nuestro universo.", "We have described much of what exists, but we still do not know why these particles, these fields and these laws form our universe.", "Olemme kuvanneet suuren osan olemassa olevasta, mutta emme vieläkään tiedä, miksi juuri nämä hiukkaset, kentät ja lait muodostavat maailmankaikkeutemme.", 11480, 11360, 11960)),
                new RawZoneSeed(14, 0, "frontier", new Color(0.7451f, 0.8706f, 1.0000f, 1f), new CosmosZoneSoundRecipe(43.00f, 460.0f, 0.0750f, 0.0200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Frontera de la física fundamental", "Frontier of fundamental physics", "Perusfysiikan raja", 2920, 2680, 1420),
                    new CosmosBilingualText("Punto de partida · campos cuánticos + gravedad clásica", "Starting point · quantum fields + classical gravity", "Lähtöpiste · kvanttikentät + klassinen painovoima", 4720, 5320, 5520),
                    new CosmosBilingualText("El Modelo Estándar describe partículas y campos; la relatividad general describe la gravedad. El recorrido comienza en la pregunta que todavía los separa: ¿qué clase de realidad es el espacio-tiempo?", "The Standard Model describes particles and fields; General relativity describes gravity. The journey begins with the question that still separates them: what kind of reality is space-time?", "Standardimalli kuvaa hiukkasia ja kenttiä, ja yleinen suhteellisuusteoria kuvaa painovoimaa. Matka alkaa kysymyksestä, joka yhä erottaa ne: millainen todellisuus aika-avaruus on?", 15040, 14120, 14680)),
                new RawZoneSeed(14, 4, "spacetime", new Color(0.4314f, 0.6118f, 1.0000f, 1f), new CosmosZoneSoundRecipe(82.00f, 1460.0f, 0.0260f, 0.1400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("El espacio-tiempo", "Spacetime", "aika-avaruus", 1720, 850, 1140),
                    new CosmosBilingualText("Tres dimensiones espaciales + una dimensión temporal", "Three spatial dimensions + one temporal dimension", "Kolme spatiaalista ulottuvuutta + yksi ajallinen ulottuvuus", 4800, 4040, 5840),
                    new CosmosBilingualText("Un objeto no es solo un punto que cambia de posición: traza una línea de universo. Distintos observadores usan coordenadas diferentes para la misma historia física.", "An object is not just a point that changes position: it traces a worldline. Different observers use different coordinates for the same physical history.", "Kappale ei ole vain sijaintiaan muuttava piste, vaan se piirtää maailmanviivan. Eri havaitsijat käyttävät samasta fysikaalisesta historiasta eri koordinaatteja.", 11920, 11840, 12440)),
                new RawZoneSeed(14, 7, "blackholes", new Color(1.0000f, 0.5373f, 0.4000f, 1f), new CosmosZoneSoundRecipe(111.00f, 1980.0f, 0.0650f, 0.2400f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Agujeros negros", "Black holes", "Mustat aukot", 1480, 1040, 1230),
                    new CosmosBilingualText("Curvatura extrema · horizonte sin superficie material", "Extreme curvature · horizon without material surface", "Äärimmäinen kaarevuus · horisontti ilman materiaalipintaa", 4400, 5640, 5640),
                    new CosmosBilingualText("Un agujero negro contiene una región causal separada por un horizonte. Las observaciones apoyan con fuerza la existencia de objetos compactos con horizontes, pero su interior profundo permanece fuera del acceso directo.", "A black hole contains a causal region separated by a horizon. Observations strongly support the existence of compact objects with horizons, but their deep interior remains out of direct access.", "Mustassa aukossa on tapahtumahorisontin erottama kausaalinen alue. Havainnot tukevat vahvasti horisontillisten kompaktien kohteiden olemassaoloa, mutta niiden syviin sisäosiin ei ole suoraa pääsyä.", 15400, 15560, 14720)),
                new RawZoneSeed(14, 11, "planck", new Color(0.9059f, 0.9333f, 1.0000f, 1f), new CosmosZoneSoundRecipe(154.00f, 2880.0f, 0.0750f, 0.3600f, CosmosWaveform.Square),
                    new CosmosBilingualText("La escala de Planck", "The Planck scale", "Planckin asteikko", 1640, 1200, 1710),
                    new CosmosBilingualText("Geometría clásica posiblemente insuficiente", "Classical geometry possibly insufficient", "Klassinen geometria saattaa olla riittämätön", 3680, 3360, 3680),
                    new CosmosBilingualText("A escalas de Planck, combinar gravedad y incertidumbre cuántica indica que la imagen de un espacio-tiempo suave puede dejar de ser válida. No sabemos qué estructura la reemplaza.", "At Planck scales, combining gravity and quantum uncertainty indicates that the picture of a smooth spacetime may no longer be valid. We do not know what structure replaces it.", "Planckin asteikolla painovoiman ja kvanttiepävarmuuden yhdistäminen osoittaa, että kuva tasaisesta aika-avaruudesta ei ehkä ole enää voimassa. Emme tiedä, mikä rakenne sen korvaa.", 13760, 12600, 13480)),
                new RawZoneSeed(14, 12, "quantumgravity", new Color(0.8353f, 0.5765f, 1.0000f, 1f), new CosmosZoneSoundRecipe(164.00f, 3160.0f, 0.0650f, 0.3900f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Caminos hacia la gravedad cuántica", "Paths to quantum gravity", "Polut kvanttigravitaatioon", 3000, 2000, 2160),
                    new CosmosBilingualText("Continuidad · discreción · unificación · causalidad · holografía", "Continuity · discreteness · unification · causality · holography", "Jatkuvuus · diskreettisyys · yhdistäminen · kausaalisuus · holografia", 7200, 6640, 8680),
                    new CosmosBilingualText("Las propuestas exploran respuestas distintas y a veces complementarias. La aplicación no las clasifica como ganadoras: la evidencia todavía no identifica una teoría microscópica correcta del espacio-tiempo.", "The proposals explore different and sometimes complementary answers. This app does not rank them: evidence has not yet identified the correct microscopic theory of spacetime.", "Ehdotukset tutkivat erilaisia ja joskus toisiaan täydentäviä vastauksia. Sovellus ei aseta niitä paremmuusjärjestykseen, sillä havainnot eivät vielä osoita oikeaa aika-avaruuden mikroskooppista teoriaa.", 13680, 12240, 14160)),
                new RawZoneSeed(14, 14, "origin", new Color(0.9686f, 0.9765f, 1.0000f, 1f), new CosmosZoneSoundRecipe(184.00f, 3800.0f, 0.0250f, 0.4600f, CosmosWaveform.Sine),
                    new CosmosBilingualText("El origen del espacio y del tiempo", "The origin of space and time", "Avaruuden ja ajan alkuperä", 3320, 3120, 2840),
                    new CosmosBilingualText("Más allá de la geometría confirmada", "Beyond confirmed geometry", "Vahvistetun geometrian ulkopuolella", 2720, 2080, 2650),
                    new CosmosBilingualText("La relatividad describe con enorme precisión cómo evoluciona el espacio-tiempo. La física cuántica describe partículas y campos. Todavía no poseemos una teoría confirmada que explique completamente cómo ambas descripciones forman una sola realidad.", "Relativity describes with enormous precision how spacetime evolves. Quantum physics describes particles and fields. We still lack a confirmed theory that fully explains how both descriptions form a single reality.", "Suhteellisuusteoria kuvaa valtavalla tarkkuudella, kuinka aika-avaruus kehittyy. Kvanttifysiikka kuvaa hiukkasia ja kenttiä. Meillä ei vieläkään ole vahvistettua teoriaa, joka selittää täysin, kuinka molemmat kuvaukset muodostavat yhden todellisuuden.", 20160, 16720, 19120)),
                new RawZoneSeed(15, 0, "reality", new Color(0.6902f, 0.8314f, 1.0000f, 1f), new CosmosZoneSoundRecipe(43.00f, 470.0f, 0.0550f, 0.0200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("La naturaleza de la realidad", "The nature of reality", "Todellisuuden luonne", 2200, 2160, 1860),
                    new CosmosBilingualText("Punto de partida · de lo fundamental a lo emergente", "Starting point · from the fundamental to the emergent", "Lähtökohta · perustavasta emergenttiin", 3880, 4520, 4200),
                    new CosmosBilingualText("El recorrido anterior terminó preguntando qué es fundamental. Este comienza con la dirección inversa: cómo pueden surgir organización, función y novedad colectiva a partir de reglas e ingredientes más simples.", "The previous tour ended by asking what is fundamental. This begins with the reverse direction: how organization, function, and collective novelty can emerge from simpler rules and ingredients.", "Edellinen kiertue päättyi kysymällä, mikä on perustavanlaatuista. Tämä alkaa päinvastaisesta suunnasta: kuinka organisaatio, toiminta ja kollektiivinen uutuus voivat syntyä yksinkertaisemmista säännöistä ja aineksista.", 15120, 14600, 16880)),
                new RawZoneSeed(15, 6, "patterns", new Color(0.5922f, 0.9647f, 0.4549f, 1f), new CosmosZoneSoundRecipe(86.00f, 1580.0f, 0.0750f, 0.1800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Patrones espontáneos", "Spontaneous patterns", "Spontaanit kuviot", 1920, 1480, 1380),
                    new CosmosBilingualText("Ondas · espirales · franjas · manchas · ramificación", "Waves · spirals · stripes · spots · branching", "Aallot · spiraalit · raidat · täplät · haarautuminen", 7160, 5560, 6080),
                    new CosmosBilingualText("El orden puede aparecer sin un controlador central. Interacciones locales, difusión, reacción y flujo pueden producir formas globales regulares.", "Order can appear without a central controller. Local interactions, diffusion, reaction and flow can produce regular global shapes.", "Järjestys voi syntyä ilman keskitettyä ohjausta. Paikalliset vuorovaikutukset, diffuusio, reaktiot ja virtaukset voivat tuottaa säännöllisiä kokonaismuotoja.", 10840, 10800, 12880)),
                new RawZoneSeed(15, 8, "networks", new Color(1.0000f, 0.7216f, 0.3176f, 1f), new CosmosZoneSoundRecipe(103.00f, 1980.0f, 0.0520f, 0.2800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Redes complejas", "Complex networks", "Monimutkaiset verkot", 1520, 1260, 1510),
                    new CosmosBilingualText("Nodos + conexiones → rutas, centros, cascadas", "Nodes + connections → paths, hubs, cascades", "Solmut + yhteydet → reitit, keskukset, kaskadit", 6160, 4400, 5480),
                    new CosmosBilingualText("La organización de las conexiones puede importar tanto como la naturaleza de los componentes. La misma lógica de red aparece en moléculas, neuronas, ecosistemas, redes eléctricas y sociedades.", "The organization of the connections can matter as much as the nature of the components. The same network logic appears in molecules, neurons, ecosystems, electrical networks, and societies.", "Yhteyksien organisoinnilla voi olla yhtä paljon merkitystä kuin komponenttien luonteella. Sama verkkologiikka esiintyy molekyyleissä, hermosoluissa, ekosysteemeissä, sähköverkoissa ja yhteiskunnissa.", 15120, 15040, 15040)),
                new RawZoneSeed(15, 11, "chemistry", new Color(0.9373f, 0.3608f, 0.7647f, 1f), new CosmosZoneSoundRecipe(133.00f, 2670.0f, 0.0500f, 0.4300f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Química organizada", "Organized chemistry", "Järjestäytynyt kemia", 2040, 1520, 1660),
                    new CosmosBilingualText("Catálisis · ciclos · reconocimiento · compartimentos", "Catalysis · cycles · recognition · compartments", "Katalyysi · syklit · tunnistus · lokerot", 6600, 5480, 5080),
                    new CosmosBilingualText("Las moléculas pueden formar ciclos mutuamente sostenidos en los que los productos de unas reacciones facilitan otras. La organización química convierte una lista de sustancias en una red dinámica.", "Molecules can form mutually sustained cycles in which the products of some reactions facilitate others. Chemical organization turns a list of substances into a dynamic network.", "Molekyylit voivat muodostaa toisiaan ylläpitäviä syklejä, joissa joidenkin reaktioiden tuotteet edistävät toisia reaktioita. Kemiallinen järjestäytyminen muuttaa aineluettelon dynaamiseksi verkostoksi.", 13960, 14480, 14440)),
                new RawZoneSeed(15, 15, "threshold", new Color(0.4980f, 1.0000f, 0.6706f, 1f), new CosmosZoneSoundRecipe(187.00f, 3800.0f, 0.0350f, 0.7000f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("El umbral de la vida", "The threshold of life", "Elämän kynnys", 1920, 2160, 1460),
                    new CosmosBilingualText("Automantenimiento + herencia + variación + evolución", "Self-maintenance + inheritance + variation + evolution", "Itsensä ylläpito + periytyminen + muuntelu + evoluutio", 6480, 6680, 7320),
                    new CosmosBilingualText("La física permite la química, y la química puede generar redes que se organizan, conservan información y se reproducen. Aún no sabemos qué secuencia concreta transformó materia no viva en los primeros sistemas biológicos.", "Physics enables chemistry, and chemistry can generate networks that organize, conserve information, and reproduce. We still do not know what specific sequence transformed non-living matter into the first biological systems.", "Fysiikka mahdollistaa kemian, ja kemia voi synnyttää verkostoja, jotka järjestäytyvät, säilyttävät informaatiota ja lisääntyvät. Emme vielä tiedä, mikä tapahtumaketju muutti elottoman aineen ensimmäisiksi biologisiksi järjestelmiksi.", 16560, 16520, 18000)),
                new RawZoneSeed(16, 0, "threshold", new Color(0.3882f, 1.0000f, 0.7451f, 1f), new CosmosZoneSoundRecipe(46.00f, 520.0f, 0.0600f, 0.0300f, CosmosWaveform.Sine),
                    new CosmosBilingualText("El umbral de la vida", "The threshold of life", "Elämän kynnys", 1920, 2240, 1380),
                    new CosmosBilingualText("Compartimento + energía + copia + variación", "Compartment + energy + copy + variation", "Lokero + energia + kopiointi + muuntelu", 5160, 6960, 5840),
                    new CosmosBilingualText("No conocemos el instante exacto ni la ruta concreta por la que comenzó la vida. Los primeros sistemas vivos pudieron surgir mediante transiciones graduales entre química organizada y evolución biológica.", "We do not know the exact moment or the specific route by which life began. The first living systems could have emerged through gradual transitions between organized chemistry and biological evolution.", "Emme tiedä tarkkaa hetkeä tai reittiä, jota pitkin elämä alkoi. Ensimmäiset elävät järjestelmät olisivat voineet syntyä asteittaisten siirtymien kautta järjestäytyneen kemian ja biologisen evoluution välillä.", 14440, 15600, 15520)),
                new RawZoneSeed(16, 2, "cells", new Color(0.2157f, 0.8627f, 0.6902f, 1f), new CosmosZoneSoundRecipe(59.00f, 850.0f, 0.0500f, 0.0800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Células primitivas", "primitive cells", "primitiiviset solut", 1920, 1320, 1530),
                    new CosmosBilingualText("Membrana · interior · energía · reparación · división", "membrane · interior · energy · repair · division", "Kalvo · sisäpuoli · energia · korjaus · jakautuminen", 7040, 6480, 6120),
                    new CosmosBilingualText("La célula integra frontera, química interna, transporte, mantenimiento, reproducción y herencia. Las primeras células debieron ser mucho más simples que las actuales.", "The cell integrates border, internal chemistry, transport, maintenance, reproduction and inheritance. The first cells must have been much simpler than today's cells.", "Solu yhdistää rajan, sisäisen kemian, kuljetuksen, ylläpidon, lisääntymisen ja periytymisen. Ensimmäisten solujen on täytynyt olla paljon yksinkertaisempia kuin nykyiset solut.", 13440, 12320, 15000)),
                new RawZoneSeed(16, 5, "evolution", new Color(0.9608f, 0.8706f, 0.2902f, 1f), new CosmosZoneSoundRecipe(83.00f, 1480.0f, 0.0500f, 0.2200f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Evolución darwiniana", "Darwinian evolution", "Darwinilainen evoluutio", 2040, 1800, 2070),
                    new CosmosBilingualText("Variación + herencia + reproducción diferencial + generaciones", "Variation + inheritance + differential reproduction + generations", "Muuntelu + periytyminen + erilainen lisääntymismenestys + sukupolvet", 6520, 7160, 7640),
                    new CosmosBilingualText("Los individuos no evolucionan durante su vida; evolucionan las poblaciones a través de generaciones. Selección, deriva, mutación, migración y cooperación cambian la historia de los linajes.", "Individuals do not evolve during their lifetimes; populations evolve over generations. Selection, drift, mutation, migration, and cooperation shape the history of lineages.", "Yksilöt eivät evolvoidu elämänsä aikana, vaan populaatiot evolvoituvat sukupolvien kuluessa. Valinta, ajautuminen, mutaatiot, muuttoliike ja yhteistyö muovaavat sukulinjojen historiaa.", 14720, 15000, 14840)),
                new RawZoneSeed(16, 7, "complexcells", new Color(1.0000f, 0.5137f, 0.3373f, 1f), new CosmosZoneSoundRecipe(104.00f, 1970.0f, 0.0400f, 0.3400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Células complejas", "Complex cells", "Monimutkaiset solut", 1880, 1000, 1640),
                    new CosmosBilingualText("Núcleo + orgánulos + membranas internas + endosimbiosis", "Nucleus + organelles + internal membranes + endosymbiosis", "Ydin + organellit + sisäiset kalvot + endosymbioosi", 6680, 5960, 4560),
                    new CosmosBilingualText("Las células eucariotas integraron compartimentos internos y antiguos socios microbianos. Las mitocondrias descienden de bacterias que se volvieron partes permanentes de otra célula.", "Eukaryotic cells integrated internal compartments and ancient microbial partners. Mitochondria descend from bacteria that became permanent parts of another cell.", "Eukaryoottisolut integroivat sisäisiä osastoja ja muinaisia mikrobikumppaneita. Mitokondriot ovat peräisin bakteereista, joista on tullut toisen solun pysyviä osia.", 12000, 12120, 12480)),
                new RawZoneSeed(16, 9, "multicellular", new Color(0.9216f, 0.3569f, 0.7922f, 1f), new CosmosZoneSoundRecipe(129.00f, 2460.0f, 0.0400f, 0.4700f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Multicelularidad", "Multicellularity", "Monisoluisuus", 2400, 1460, 1350),
                    new CosmosBilingualText("Adhesión + comunicación + cooperación + especialización", "Adhesion + communication + cooperation + specialization", "Kiinnitys + viestintä + yhteistyö + erikoistuminen", 6120, 6280, 6680),
                    new CosmosBilingualText("Un organismo multicelular no es solo una colonia de células idénticas. Sus células coordinan crecimiento, división de tareas, reparación, reproducción y muerte programada.", "A multicellular organism is not just a colony of identical cells. Its cells coordinate growth, division of tasks, repair, reproduction and programmed death.", "Monisoluinen organismi ei ole vain identtisten solujen pesäke. Sen solut koordinoivat kasvua, tehtävien jakamista, korjausta, lisääntymistä ja ohjelmoitua kuolemaa.", 14160, 12960, 13800)),
                new RawZoneSeed(16, 16, "brains", new Color(0.6549f, 0.6118f, 1.0000f, 1f), new CosmosZoneSoundRecipe(236.00f, 3910.0f, 0.0350f, 0.7900f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Cerebros y aprendizaje", "Brains and learning", "Aivot ja oppiminen", 2120, 1340, 1540),
                    new CosmosBilingualText("Memoria + predicción + recompensa + plasticidad", "Memory + prediction + reward + plasticity", "Muisti + ennustus + palkinto + plastisuus", 5680, 5400, 6680),
                    new CosmosBilingualText("El aprendizaje permite que la experiencia cambie conductas futuras. Los cerebros combinan respuestas heredadas con plasticidad, memoria, predicción y selección de acciones.", "Learning allows experience to change future behaviors. Brains combine inherited responses with plasticity, memory, prediction, and action selection.", "Oppiminen antaa kokemuksen muuttaa tulevia käyttäytymismalleja. Aivot yhdistävät periytyneet vastaukset plastisuuteen, muistiin, ennustamiseen ja toimintojen valintaan.", 12960, 12320, 12320)),
                new RawZoneSeed(16, 18, "consciousness", new Color(1.0000f, 0.8627f, 0.6431f, 1f), new CosmosZoneSoundRecipe(281.00f, 4500.0f, 0.0220f, 0.9000f, CosmosWaveform.Sine),
                    new CosmosBilingualText("La experiencia consciente", "The conscious experience", "Tietoinen kokemus", 2320, 1870, 1520),
                    new CosmosBilingualText("Actividad cerebral → experiencia subjetiva aún no explicada por completo", "Brain activity → subjective experience not yet fully explained", "Aivojen toiminta → subjektiivinen kokemus, jota ei ole vielä täysin selitetty", 6440, 5480, 5760),
                    new CosmosBilingualText("La evolución produjo organismos capaces de sentir, aprender y construir modelos de su entorno. Todavía no comprendemos por completo cómo la actividad física de un cerebro produce experiencia subjetiva.", "Evolution produced organisms capable of sensing, learning, and building models of their environment. We still do not fully understand how the physical activity of a brain produces subjective experience.", "Evoluutio tuotti organismeja, jotka pystyivät aistimaan, oppimaan ja rakentamaan malleja ympäristöstään. Emme vieläkään täysin ymmärrä, kuinka aivojen fyysinen toiminta tuottaa subjektiivista kokemusta.", 14720, 15720, 14800)),
                new RawZoneSeed(17, 0, "consciousness", new Color(1.0000f, 0.8627f, 0.6431f, 1f), new CosmosZoneSoundRecipe(58.00f, 780.0f, 0.0180f, 0.0800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("La experiencia consciente", "The conscious experience", "Tietoinen kokemus", 2320, 1980, 1500),
                    new CosmosBilingualText("Vigilia + experiencia + cuerpo + atención + actividad neural", "Wakefulness + experience + body + attention + neural activity", "Valvetila + kokemus + keho + tarkkaavaisuus + hermotoiminta", 8720, 8560, 8320),
                    new CosmosBilingualText("La experiencia consciente es el punto de partida de este viaje, no una explicación terminada. Experimentar algo no es lo mismo que comprenderlo, recordarlo o expresarlo.", "Conscious experience is the starting point of this journey, not a finished explanation. Experiencing something is not the same as understanding it, remembering it, or expressing it.", "Tietoinen kokemus on tämän matkan lähtökohta, ei valmis selitys. Jonkin kokeminen ei ole sama asia kuin sen ymmärtäminen, muistaminen tai ilmaiseminen.", 13560, 13200, 12160)),
                new RawZoneSeed(17, 2, "perception", new Color(0.6902f, 0.6392f, 1.0000f, 1f), new CosmosZoneSoundRecipe(74.00f, 1370.0f, 0.0260f, 0.1800f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Percepción", "Perception", "Havainto", 1360, 970, 900),
                    new CosmosBilingualText("Señales incompletas + contexto + predicción → interpretación", "Incomplete signals + context + prediction → interpretation", "Epätäydelliset signaalit + konteksti + ennustus → tulkinta", 5960, 5280, 5080),
                    new CosmosBilingualText("La percepción organiza señales ambiguas en formas, objetos, movimientos y escenas. Es una interpretación activa y útil, no una fotografía perfecta de la realidad.", "Perception organizes ambiguous signals into shapes, objects, movements, and scenes. It is an active and useful interpretation, not a perfect photograph of reality.", "Havainto järjestää moniselitteiset signaalit muodoiksi, esineiksi, liikkeiksi ja kohtauksiksi. Se on aktiivinen ja hyödyllinen tulkinta, ei täydellinen valokuva todellisuudesta.", 15240, 13160, 14240)),
                new RawZoneSeed(17, 8, "language", new Color(0.9373f, 0.5686f, 1.0000f, 1f), new CosmosZoneSoundRecipe(142.00f, 2860.0f, 0.0320f, 0.6100f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Lenguaje", "Language", "Kieli", 800, 590, 530),
                    new CosmosBilingualText("Sonidos o gestos + símbolos + reglas + diálogo", "Sounds or gestures + symbols + rules + dialogue", "Äänet tai eleet + symbolit + säännöt + dialogi", 6520, 6800, 6280),
                    new CosmosBilingualText("El lenguaje permite referirse a lo ausente, coordinar perspectivas y conservar ideas. No solo etiqueta pensamientos terminados: también reorganiza memoria, abstracción y cooperación.", "Language allows us to refer to what is absent, coordinate perspectives and preserve ideas. It not only labels finished thoughts: it also reorganizes memory, abstraction and cooperation.", "Kielen avulla voimme viitata puuttuvaan, koordinoida näkökulmia ja säilyttää ideoita. Se ei vain merkitse valmiita ajatuksia: se myös järjestää uudelleen muistin, abstraktion ja yhteistyön.", 14480, 14680, 14960)),
                new RawZoneSeed(17, 9, "reasoning", new Color(0.3961f, 0.7843f, 1.0000f, 1f), new CosmosZoneSoundRecipe(156.00f, 3100.0f, 0.0150f, 0.6700f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Razonamiento", "Reasoning", "Päättely", 1200, 730, 690),
                    new CosmosBilingualText("Premisas + relaciones + método → conclusión provisional", "Premises + relationships + method → provisional conclusion", "Premissit + suhteet + menetelmä → alustava johtopäätös", 6000, 5640, 6120),
                    new CosmosBilingualText("Razonar conecta ideas para producir inferencias. No garantiza una conclusión correcta: depende de premisas, evidencia, métodos y de cómo se representan las alternativas.", "Reasoning connects ideas to produce inferences. It does not guarantee a correct conclusion: it depends on premises, evidence, methods and how alternatives are represented.", "Päättely yhdistää ideat johtopäätösten tuottamiseksi. Se ei takaa oikeaa johtopäätöstä: se riippuu lähtökohdista, todisteista, menetelmistä ja siitä, miten vaihtoehdot esitetään.", 12680, 13640, 12600)),
                new RawZoneSeed(17, 14, "metacognition", new Color(0.8078f, 0.8784f, 1.0000f, 1f), new CosmosZoneSoundRecipe(236.00f, 4040.0f, 0.0120f, 0.9000f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Metacognición", "Metacognition", "Metakognitio", 1560, 1430, 1260),
                    new CosmosBilingualText("Estimación + control + revisión + cambio de estrategia", "Estimation + control + review + strategy change", "Arviointi + ohjaus + tarkistus + strategian muutos", 6440, 5160, 7000),
                    new CosmosBilingualText("La metacognición permite evaluar confianza, detectar errores y elegir estrategias. No ofrece acceso perfecto a la propia mente: también puede estar sesgada o equivocada.", "Metacognition allows you to evaluate confidence, detect errors and choose strategies. It does not offer perfect access to one's own mind: it can also be biased or wrong.", "Metakognition avulla voi arvioida varmuutta, havaita virheitä ja valita strategioita. Se ei anna täydellistä pääsyä omaan mieleen, vaan voi itsekin olla vinoutunut tai erehtynyt.", 12840, 13600, 14600)),
                new RawZoneSeed(17, 17, "knowledge", new Color(1.0000f, 0.9529f, 0.8275f, 1f), new CosmosZoneSoundRecipe(286.00f, 4560.0f, 0.0090f, 0.9900f, CosmosWaveform.Sine),
                    new CosmosBilingualText("El conocimiento compartido", "Shared knowledge", "Jaettu tieto", 2280, 1400, 1040),
                    new CosmosBilingualText("Observación + registro + prueba + crítica + revisión", "Observation + registration + test + criticism + review", "Havainto + kirjaaminen + koe + kritiikki + tarkistus", 6160, 7040, 8640),
                    new CosmosBilingualText("Cuando muchas mentes comparten símbolos, corrigen errores y conservan descubrimientos, el pensamiento individual se convierte en cultura y conocimiento acumulativo.", "When many minds share symbols, correct errors, and preserve discoveries, individual thought becomes culture and cumulative knowledge.", "Kun monet mielet jakavat symboleja, korjaavat virheet ja säilyttävät löydöt, yksilöllisestä ajattelusta tulee kulttuuria ja kumulatiivista tietoa.", 13040, 10800, 12520)),
                new RawZoneSeed(18, 0, "shared-knowledge", new Color(1.0000f, 0.8275f, 0.5373f, 1f), new CosmosZoneSoundRecipe(58.00f, 760.0f, 0.0180f, 0.0700f, CosmosWaveform.Sine),
                    new CosmosBilingualText("El conocimiento compartido", "Shared knowledge", "Jaettu tieto", 2280, 1120, 1200),
                    new CosmosBilingualText("Experiencia + símbolos + enseñanza + corrección → conocimiento público", "Experience + symbols + teaching + correction → public knowledge", "Kokemus + symbolit + opetus + korjaus → julkinen tieto", 8160, 7280, 7040),
                    new CosmosBilingualText("Compartir información no garantiza que sea verdadera, pero permite conservarla, compararla, corregirla y ampliarla.", "Sharing information does not guarantee that it is true, but it allows it to be preserved, compared, corrected and expanded.", "Tiedon jakaminen ei takaa niiden paikkansapitävyyttä, mutta se mahdollistaa sen säilyttämisen, vertailun, korjaamisen ja laajentamisen.", 8720, 9600, 9920)),
                new RawZoneSeed(18, 8, "art", new Color(0.8784f, 0.4627f, 1.0000f, 1f), new CosmosZoneSoundRecipe(90.80f, 1600.0f, 0.0330f, 0.1050f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Arte y simbolismo", "Art and symbolism", "Taide ja symboliikka", 1760, 1540, 1710),
                    new CosmosBilingualText("Forma + ritmo + materia + contexto → significado cultural", "Form + rhythm + matter + context → cultural meaning", "Muoto + rytmi + aine + konteksti → kulttuurinen merkitys", 7360, 6800, 6880),
                    new CosmosBilingualText("El arte puede comunicar emoción, identidad, memoria y significado sin funcionar como una descripción literal.", "Art can communicate emotion, identity, memory, and meaning without functioning as literal description.", "Taide voi viestiä tunteita, identiteettiä, muistia ja merkitystä toimimatta kirjaimellisena kuvauksena.", 9480, 8440, 8760)),
                new RawZoneSeed(18, 10, "agriculture", new Color(0.5176f, 0.8039f, 0.3686f, 1f), new CosmosZoneSoundRecipe(99.00f, 1810.0f, 0.0180f, 0.1750f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Agricultura", "Agriculture", "Maatalous", 1320, 770, 750),
                    new CosmosBilingualText("Selección + calendario + trabajo + almacenamiento → producción planificada", "Selection + calendar + work + storage → planned production", "Valinta + kalenteri + työ + varastointi → suunniteltu tuotanto", 8760, 8400, 8360),
                    new CosmosBilingualText("La agricultura surgió de manera independiente en varias regiones y produjo beneficios, costes y trayectorias diferentes; no fue una mejora inmediata en todo aspecto de la vida.", "Agriculture emerged independently in various regions and produced different benefits, costs and trajectories; It was not an immediate improvement in every aspect of life.", "Maatalous syntyi itsenäisesti eri alueilla ja tuotti erilaisia hyötyjä, kustannuksia ja kehityskulkuja; Se ei ollut välitön parannus kaikilla elämän osa-alueilla.", 12800, 13000, 13000)),
                new RawZoneSeed(18, 12, "writing", new Color(0.9608f, 0.8549f, 0.6706f, 1f), new CosmosZoneSoundRecipe(107.20f, 2020.0f, 0.0280f, 0.2450f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Escritura", "Writing", "Kirjoittaminen", 750, 660, 1080),
                    new CosmosBilingualText("Marcas convencionales + soporte + lectura → registro transmisible", "Conventional marks + medium + reading → transferable record", "Sovitut merkit + tallennusalusta + lukeminen → välitettävä merkintä", 6720, 6560, 7520),
                    new CosmosBilingualText("La escritura permite almacenar mensajes con mayor precisión y transmitirlos a distancia y entre generaciones, pero las sociedades sin escritura también conservan conocimiento complejo.", "Writing allows messages to be stored with greater precision and transmitted over distance and between generations, but societies without writing also preserve complex knowledge.", "Kirjoittaminen mahdollistaa viestien tarkemman tallentamisen ja välittämisen etäisyyden ja sukupolvien välillä, mutta yhteiskunnat, joissa ei ole kirjoitusta, säilyttävät myös monimutkaisen tiedon.", 12880, 12600, 14480)),
                new RawZoneSeed(18, 13, "cities", new Color(1.0000f, 0.7333f, 0.3216f, 1f), new CosmosZoneSoundRecipe(111.30f, 2125.0f, 0.0330f, 0.2800f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Ciudades", "Cities", "kaupungit", 1200, 710, 830),
                    new CosmosBilingualText("Alimentos + agua + trabajo + administración + energía → sistema urbano", "Food + water + work + administration + energy → urban system", "Ruoka + vesi + työ + hallinto + energia → kaupunkijärjestelmä", 11880, 8160, 7080),
                    new CosmosBilingualText("Una ciudad no es un solo invento: es una red dependiente de territorios, infraestructuras, trabajo, autoridad y flujos materiales.", "A city is not a single invention: it is a dependent network of territories, infrastructure, work, authority and material flows.", "Kaupunki ei ole yksittäinen keksintö: se on riippuvainen alueiden, infrastruktuurin, työn, vallan ja materiaalivirtojen verkosto.", 10080, 11400, 9480)),
                new RawZoneSeed(18, 19, "planetary-civilization", new Color(0.6667f, 0.9373f, 1.0000f, 1f), new CosmosZoneSoundRecipe(135.90f, 2755.0f, 0.0380f, 0.2450f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Civilización planetaria", "planetary civilization", "planetaarinen sivilisaatio", 2120, 1780, 1940),
                    new CosmosBilingualText("Sociedades conectadas + energía + conocimiento + riesgos → capacidad planetaria", "Connected societies + energy + knowledge + risks → planetary capacity", "Yhdistetyt yhteiskunnat + energia + tieto + riskit → planeetan kapasiteetti", 9400, 8040, 7920),
                    new CosmosBilingualText("La humanidad forma una red planetaria de sociedades interdependientes, pero todavía no actúa como una sola comunidad coordinada.", "Humanity forms a planetary network of interdependent societies, but does not yet act as a single coordinated community.", "Ihmiskunta muodostaa toisistaan riippuvaisten yhteiskuntien planetaarisen verkoston, mutta ei vielä toimi yhtenä koordinoituna yhteisönä.", 9320, 9680, 10440)),
                new RawZoneSeed(19, 0, "planetary-civilization", new Color(0.4549f, 0.8941f, 1.0000f, 1f), new CosmosZoneSoundRecipe(54.00f, 720.0f, 0.0170f, 0.0550f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Civilización planetaria", "planetary civilization", "planetaarinen sivilisaatio", 2120, 2140, 2080),
                    new CosmosBilingualText("Ciudades + redes + satélites + ciencia + industria → capacidad planetaria desigual", "Cities + networks + satellites + science + industry → unequal planetary capacity", "Kaupungit + verkot + satelliitit + tiede + teollisuus → epätasainen planeettakapasiteetti", 9720, 8600, 10720),
                    new CosmosBilingualText("Una civilización tecnológicamente conectada no controla por completo los sistemas que ha creado ni distribuye sus beneficios de manera uniforme.", "A technologically connected civilization does not fully control the systems it has created or distribute their benefits evenly.", "Teknisesti yhdistetty sivilisaatio ei hallitse täysin luomiaan järjestelmiä tai jaa niiden etuja tasaisesti.", 10200, 9640, 8400)),
                new RawZoneSeed(19, 8, "electricity", new Color(0.4627f, 0.7765f, 1.0000f, 1f), new CosmosZoneSoundRecipe(91.20f, 1616.0f, 0.0305f, 0.0790f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Electricidad", "Electricity", "Sähkö", 1240, 820, 640),
                    new CosmosBilingualText("Generación → transformación → transmisión → uso → pérdidas y equilibrio instantáneo", "Generation → transformation → transmission → use → losses and instantaneous equilibrium", "Tuotanto → muuntaminen → siirto → käyttö → häviöt ja hetkellinen tasapaino", 9560, 10200, 7560),
                    new CosmosBilingualText("La electricidad conecta fuentes y usos distantes, pero la red debe equilibrar producción y consumo continuamente.", "Electricity connects distant sources and uses, but the grid must continually balance production and consumption.", "Sähkö yhdistää kaukaisia lähteitä ja käyttötapoja, mutta verkon on jatkuvasti tasapainotettava tuotantoa ja kulutusta.", 8600, 8720, 8840)),
                new RawZoneSeed(19, 10, "electronics", new Color(0.4745f, 1.0000f, 0.7137f, 1f), new CosmosZoneSoundRecipe(100.50f, 1840.0f, 0.0170f, 0.1270f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Electrónica", "Electronics", "Elektroniikka", 1160, 850, 1030),
                    new CosmosBilingualText("Sensores + semiconductores + circuitos → detección, decisión y control", "Sensors + semiconductors + circuits → detection, decision and control", "Anturit + puolijohteet + piirit → tunnistus, päätös ja ohjaus", 8400, 7680, 7000),
                    new CosmosBilingualText("La electrónica transforma señales débiles en medición, comunicación, memoria y acción controlada.", "Electronics transform weak signals into measurement, communication, memory and controlled action.", "Elektroniikka muuntaa heikot signaalit mittaukseksi, tiedonsiirroksi, muistiksi ja ohjatuksi toiminnaksi.", 8200, 8120, 8320)),
                new RawZoneSeed(19, 11, "computing", new Color(0.5882f, 0.6588f, 1.0000f, 1f), new CosmosZoneSoundRecipe(105.15f, 1952.0f, 0.0215f, 0.1510f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Computación", "Computing", "Tietojenkäsittely", 990, 600, 1430),
                    new CosmosBilingualText("Estados físicos + lógica + memoria + instrucciones → cómputo", "Physical states + logic + memory + instructions → computation", "Fysikaaliset tilat + logiikka + muisti + ohjeet → laskenta", 7120, 7000, 8040),
                    new CosmosBilingualText("Una computadora no comprende automáticamente lo que procesa: ejecuta operaciones físicas que representan símbolos según reglas diseñadas.", "A computer does not automatically understand what it processes: it executes physical operations that represent symbols according to designed rules.", "Tietokone ei automaattisesti ymmärrä, mitä se käsittelee: se suorittaa fyysisiä toimintoja, jotka edustavat symboleja suunniteltujen sääntöjen mukaisesti.", 8960, 11880, 11200)),
                new RawZoneSeed(19, 16, "artificial-intelligence", new Color(1.0000f, 0.5294f, 0.9059f, 1f), new CosmosZoneSoundRecipe(128.40f, 2512.0f, 0.0215f, 0.1030f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Inteligencia artificial", "Artificial intelligence", "Tekoäly", 2040, 1560, 830),
                    new CosmosBilingualText("Datos + objetivo + modelo + cómputo + evaluación → comportamiento aprendido", "Data + objective + model + computation + evaluation → learned behavior", "Data + tavoite + malli + laskenta + arviointi → opittu käyttäytyminen", 10520, 9160, 9520),
                    new CosmosBilingualText("La inteligencia artificial puede producir resultados complejos sin poseer necesariamente conciencia, comprensión humana o juicio fiable.", "Artificial intelligence can produce complex results without necessarily possessing consciousness, human understanding, or reliable judgment.", "Tekoäly voi tuottaa monimutkaisia tuloksia ilman, että sillä välttämättä on tietoisuutta, ihmisen ymmärrystä tai luotettavaa arvostelukykyä.", 9240, 10440, 10240)),
                new RawZoneSeed(19, 19, "technological-futures", new Color(0.9020f, 0.9412f, 1.0000f, 1f), new CosmosZoneSoundRecipe(142.35f, 2848.0f, 0.0350f, 0.1750f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Futuros tecnológicos", "Technological futures", "Teknologiset tulevaisuudet", 1960, 1740, 1690),
                    new CosmosBilingualText("Capacidades + objetivos + poder + gobernanza + azar → futuros divergentes", "Capabilities + objectives + power + governance + chance → divergent futures", "Kyvyt + tavoitteet + valta + hallinta + sattuma → eriytyvät tulevaisuudet", 8760, 7680, 11080),
                    new CosmosBilingualText("El futuro dependerá no solo de lo que podamos construir, sino de qué objetivos elijamos, quién controle los sistemas y cómo gobernemos sus consecuencias.", "The future will depend not only on what we can build, but on what objectives we choose, who controls the systems and how we govern their consequences.", "Tulevaisuus ei riipu vain siitä, mitä voimme rakentaa, vaan siitä, mitä tavoitteita valitsemme, kuka hallitsee järjestelmiä ja miten hallitsemme niiden seurauksia.", 11240, 12600, 12680)),
                new RawZoneSeed(20, 0, "technological-futures", new Color(0.8863f, 0.9412f, 1.0000f, 1f), new CosmosZoneSoundRecipe(52.00f, 700.0f, 0.0160f, 0.0500f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Futuros tecnológicos", "Technological futures", "Teknologiset tulevaisuudet", 1960, 1730, 1960),
                    new CosmosBilingualText("Tendencias + posibilidades físicas + decisiones humanas → escenarios abiertos", "Trends + physical possibilities + human decisions → open scenarios", "Trendit + fyysiset mahdollisuudet + inhimilliset päätökset → avoimet skenaariot", 7160, 7520, 8520),
                    new CosmosBilingualText("Las proyecciones del futuro no son profecías: cartografían posibilidades condicionadas por decisiones todavía abiertas.", "Projections of the future are not prophecies: they map possibilities conditioned by decisions that are still open.", "Tulevaisuuden ennusteet eivät ole ennustuksia, vaan ne kartoittavat vielä avoimien päätösten ehdollisia mahdollisuuksia.", 9840, 8480, 8960)),
                new RawZoneSeed(20, 9, "planetary-governance", new Color(0.6824f, 0.7059f, 1.0000f, 1f), new CosmosZoneSoundRecipe(94.75f, 1762.0f, 0.0340f, 0.0920f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Gobernanza planetaria", "Planetary governance", "Planeetan hallinto", 2040, 1770, 1530),
                    new CosmosBilingualText("Cooperación + legitimidad + conocimiento + cumplimiento → acción a escala planetaria", "Cooperation + legitimacy + knowledge + compliance → action on a planetary scale", "Yhteistyö + legitimiteetti + tieto + noudattaminen → toiminta planeetan mittakaavassa", 9640, 9120, 9480),
                    new CosmosBilingualText("Coordinar problemas globales no implica que un único gobierno mundial sea inevitable ni necesariamente deseable.", "Coordinating global problems does not imply that a single world government is inevitable or necessarily desirable.", "Globaalien ongelmien koordinointi ei tarkoita, että yksi maailmanhallitus olisi väistämätön tai välttämättä toivottava.", 9320, 9120, 7840)),
                new RawZoneSeed(20, 12, "multiplanetary-civilization", new Color(0.6118f, 0.6431f, 1.0000f, 1f), new CosmosZoneSoundRecipe(109.00f, 2116.0f, 0.0250f, 0.1550f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Civilización multiplanetaria", "Multiplanetary civilization", "Moniplaneettinen sivilisaatio", 2760, 2560, 2280),
                    new CosmosBilingualText("Mundos habitados + autonomía local + comunicación retardada → civilización distribuida", "Inhabited worlds + local autonomy + delayed communication → distributed civilization", "Asutut maailmat + paikallinen autonomia + viivästynyt viestintä → hajautunut sivilisaatio", 10240, 7160, 8520),
                    new CosmosBilingualText("Una civilización multiplanetaria no sería una nación terrestre ampliada: las distancias crearían nuevas sociedades y formas de independencia.", "A multiplanetary civilization would not be an expanded terrestrial nation: distances would create new societies and forms of independence.", "Moniplaneettinen sivilisaatio ei olisi laajentunut maanpäällinen kansakunta: etäisyydet loisivat uusia yhteiskuntia ja itsenäisyyden muotoja.", 10520, 10800, 11320)),
                new RawZoneSeed(20, 15, "interstellar-civilization", new Color(0.5725f, 0.8039f, 1.0000f, 1f), new CosmosZoneSoundRecipe(123.25f, 2470.0f, 0.0160f, 0.0710f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Civilización interestelar", "interstellar civilization", "tähtienvälinen sivilisaatio", 2360, 2050, 2580),
                    new CosmosBilingualText("Propulsión + autonomía + tiempo + reproducción social → presencia entre estrellas", "Propulsion + autonomy + time + social reproduction → presence among stars", "Propulsio + autonomia + aika + sosiaalinen lisääntyminen → läsnäolo tähtien joukossa", 8680, 8760, 10760),
                    new CosmosBilingualText("Las distancias interestelares transforman expansión, comunicación e identidad en procesos de generaciones o milenios.", "Interstellar distances transform expansion, communication and identity in processes of generations or millennia.", "Tähtienväliset etäisyydet muuttavat laajentumista, viestintää ja identiteettiä sukupolvien tai vuosituhansien prosesseissa.", 10760, 9640, 10440)),
                new RawZoneSeed(20, 16, "kardashev-scale", new Color(1.0000f, 0.8549f, 0.4824f, 1f), new CosmosZoneSoundRecipe(128.00f, 2588.0f, 0.0205f, 0.0920f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Escalas de Kardashev", "Kardashev scales", "Kardaševin asteikko", 2080, 1390, 1800),
                    new CosmosBilingualText("Potencia controlada aproximada → escala planetaria, estelar o galáctica", "Approximate controlled power → planetary, stellar or galactic scale", "Likimääräinen ohjattu teho → planeetta-, tähti- tai galaktinen mittakaava", 6120, 6360, 6680),
                    new CosmosBilingualText("La escala de Kardashev mide aproximadamente uso de energía; no mide bienestar, moralidad, sabiduría ni inevitabilidad histórica.", "The Kardashev scale approximately measures energy use; it does not measure well-being, morality, wisdom or historical inevitability.", "Kardaševin asteikko mittaa suunnilleen energian käyttöä; se ei mittaa hyvinvointia, moraalia, viisautta tai historiallista väistämättömyyttä.", 10400, 10520, 11120)),
                new RawZoneSeed(20, 18, "unknown-horizon", new Color(0.9333f, 0.9569f, 1.0000f, 1f), new CosmosZoneSoundRecipe(137.50f, 2824.0f, 0.0295f, 0.1340f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Horizonte desconocido", "unknown horizon", "tuntematon horisontti", 2120, 1370, 1860),
                    new CosmosBilingualText("Cuanto mayor la distancia temporal → menor predicción y mayor espacio de posibilidades", "The greater the temporal distance → the less prediction and the greater the space of possibilities", "Mitä suurempi on ajallinen etäisyys → sitä vähemmän ennustetta ja sitä suurempi on mahdollisuuksien tila", 7280, 7200, 9560),
                    new CosmosBilingualText("Cuanto más lejos miramos, menos predecimos acontecimientos y más cartografiamos posibilidades.", "The further we look, the less we predict events and the more we map possibilities.", "Mitä pidemmälle katsomme, sitä vähemmän ennustamme tapahtumia ja sitä enemmän kartoitamme mahdollisuuksia.", 7480, 7040, 8000)),
                new RawZoneSeed(21, 0, "unknown-horizon", new Color(0.8824f, 0.9333f, 1.0000f, 1f), new CosmosZoneSoundRecipe(48.00f, 720.0f, 0.0120f, 0.0400f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Horizonte desconocido", "unknown horizon", "tuntematon horisontti", 2120, 1630, 1440),
                    new CosmosBilingualText("Imaginación informada + observación + incertidumbre → preguntas investigables", "Informed imagination + observation + uncertainty → researchable questions", "Tietoinen mielikuvitus + havainto + epävarmuus → tutkittavia kysymyksiä", 8680, 7880, 6880),
                    new CosmosBilingualText("La astrobiología no supone que exista vida extraterrestre: estudia qué condiciones podrían producirla y qué evidencias permitirían reconocerla.", "Astrobiology does not assume that extraterrestrial life exists: it studies what conditions could produce it and what evidence would allow it to be recognized.", "Astrobiologia ei oleta, että maan ulkopuolinen elämä olisi olemassa: se tutkii, mitkä olosuhteet voisivat tuottaa sen ja mitkä todisteet mahdollistaisivat sen tunnistamisen.", 11440, 11280, 12960)),
                new RawZoneSeed(21, 4, "habitable-zone", new Color(0.4824f, 0.9451f, 0.6627f, 1f), new CosmosZoneSoundRecipe(62.60f, 1072.0f, 0.0280f, 0.1120f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Zona habitable", "Habitable zone", "Asuttava vyöhyke", 1520, 1160, 1450),
                    new CosmosBilingualText("Distancia + luminosidad + atmósfera básica → intervalo térmico potencial", "Distance + luminosity + basic atmosphere → potential thermal range", "Etäisyys + valoisuus + perusatmosfääri → potentiaalinen lämpöalue", 7600, 6440, 7280),
                    new CosmosBilingualText("Estar dentro de la zona habitable no significa estar habitado, ni siquiera ser realmente habitable.", "Being within the habitable zone does not mean being inhabited, or even being truly habitable.", "Asumiskelpoisella vyöhykkeellä oleminen ei tarkoita sitä, että on asuttu tai edes todella asuttava.", 6920, 8600, 7960)),
                new RawZoneSeed(21, 12, "ocean-worlds", new Color(0.3765f, 0.8000f, 1.0000f, 1f), new CosmosZoneSoundRecipe(91.80f, 1776.0f, 0.0200f, 0.1300f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Mundos oceánicos", "ocean worlds", "Valtamerimaailmat", 2000, 890, 1440),
                    new CosmosBilingualText("Océano + calor de marea + química rocosa + tiempo → ambiente subterráneo potencial", "Ocean + tidal heat + rock chemistry + time → potential underground environment", "Meri + vuorovesilämpö + kivikemia + aika → mahdollinen maanalainen ympäristö", 9560, 7560, 8720),
                    new CosmosBilingualText("La habitabilidad no requiere necesariamente una superficie similar a la Tierra. Un océano protegido bajo kilómetros de hielo podría contener ambientes químicos de larga duración.", "Habitability does not necessarily require an Earth-like surface. An ocean protected under miles of ice could contain long-lasting chemical environments.", "Asuminen ei välttämättä vaadi maan kaltaista pintaa. Kilometrien jään alla suojattu valtameri voi sisältää pitkäaikaisia kemiallisia ympäristöjä.", 12480, 11080, 10840)),
                new RawZoneSeed(21, 13, "exoplanets", new Color(0.5882f, 0.7059f, 1.0000f, 1f), new CosmosZoneSoundRecipe(95.45f, 1864.0f, 0.0240f, 0.1480f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Exoplanetas", "exoplanets", "eksoplaneetat", 1480, 1060, 1070),
                    new CosmosBilingualText("Luz estelar + movimiento + modelos → masa, radio, órbita y atmósfera inferidos", "Starlight + motion + models → inferred mass, radius, orbit and atmosphere", "Tähtien valo + liike + mallit → päätelty massa, säde, rata ja ilmakehä", 8360, 8040, 9160),
                    new CosmosBilingualText("La mayoría de los exoplanetas no se observan como mundos detallados: sus propiedades se reconstruyen a partir de cambios muy pequeños en la luz de sus estrellas.", "Most exoplanets are not observed as detailed worlds: their properties are reconstructed from very small changes in the light from their stars.", "Useimpia eksoplaneettoja ei havaita yksityiskohtaisina maailmoina: niiden ominaisuudet rekonstruoidaan hyvin pienistä muutoksista niiden tähtien valossa.", 11040, 11160, 10400)),
                new RawZoneSeed(21, 19, "fermi-paradox", new Color(0.7686f, 0.8157f, 1.0000f, 1f), new CosmosZoneSoundRecipe(117.35f, 2392.0f, 0.0280f, 0.1300f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Paradoja de Fermi", "Fermi paradox", "Fermin paradoksi", 1680, 1290, 1470),
                    new CosmosBilingualText("Número de mundos × tiempo cósmico × detectabilidad → expectativa comparada con evidencia", "Number of worlds × cosmic time × detectability → expectation compared to evidence", "Maailmojen määrä × kosminen aika × havaittavuus → odotusten vertaaminen näyttöön", 8800, 9840, 8760),
                    new CosmosBilingualText("La paradoja de Fermi no demuestra que no existan otras civilizaciones. Señala una tensión entre algunas expectativas y la ausencia actual de evidencia confirmada.", "The Fermi paradox does not prove that other civilizations do not exist. It points to a tension between some expectations and the current absence of confirmed evidence.", "Fermin paradoksi ei todista, etteikö muita sivilisaatioita olisi olemassa. Se viittaa jännitteeseen joidenkin odotusten ja vahvistettujen todisteiden puuttumisen välillä.", 11680, 11840, 13880)),
                new RawZoneSeed(21, 24, "unknown-cosmic-life", new Color(0.9333f, 0.9608f, 1.0000f, 1f), new CosmosZoneSoundRecipe(135.60f, 2832.0f, 0.0280f, 0.0940f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Vida cósmica desconocida", "Unknown cosmic life", "Tuntematon kosminen elämä", 2200, 1770, 2040),
                    new CosmosBilingualText("Observación limitada + conceptos terrestres + fenómenos extraños → clasificación provisional", "Limited observation + terrestrial concepts + strange phenomena → provisional classification", "Rajalliset havainnot + Maahan perustuvat käsitteet + oudot ilmiöt → alustava luokitus", 9160, 8280, 8320),
                    new CosmosBilingualText("Buscamos vida mediante conceptos construidos a partir de un solo ejemplo: la Tierra. Cuanto más diferente sea otra forma de vida, más difícil podría ser distinguirla de la geología, la química, la tecnología o incluso del ruido.", "We search for life through concepts built from a single example: the Earth. The more different another form of life is, the harder it might be to distinguish it from geology, chemistry, technology, or even noise.", "Etsimme elämää konseptien avulla, jotka on rakennettu yhdestä esimerkistä: maapallosta. Mitä erilaisempi toinen elämänmuoto on, sitä vaikeampaa se voi olla erottaa geologiasta, kemiasta, tekniikasta tai jopa melusta.", 17320, 15640, 18640)),
                new RawZoneSeed(22, 0, "unexplained-signal", new Color(0.8824f, 0.9333f, 1.0000f, 1f), new CosmosZoneSoundRecipe(48.00f, 720.0f, 0.0100f, 0.0350f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Una señal sin explicación definitiva", "A signal without a definitive explanation", "Signaali ilman lopullista selitystä", 3320, 3880, 3160),
                    new CosmosBilingualText("Señal observada + causas posibles + incertidumbre explícita → pregunta verificable", "Observed signal + possible causes + explicit uncertainty → verifiable question", "Havaittu signaali + mahdolliset syyt + eksplisiittinen epävarmuus → todennettavissa oleva kysymys", 8520, 7400, 8640),
                    new CosmosBilingualText("Una señal extraña no es todavía evidencia de inteligencia: es un fenómeno que exige mejores datos y explicaciones alternativas.", "A strange signal is not yet evidence of intelligence: it is a phenomenon that demands better data and alternative explanations.", "Outo signaali ei ole vielä todiste älykkyydestä: se on ilmiö, joka vaatii parempaa dataa ja vaihtoehtoisia selityksiä.", 9280, 10040, 8600)),
                new RawZoneSeed(22, 6, "artificial-pattern", new Color(1.0000f, 0.8863f, 0.4392f, 1f), new CosmosZoneSoundRecipe(67.50f, 1212.0f, 0.0140f, 0.0350f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Patrón artificial", "artificial pattern", "keinotekoinen kuvio", 1960, 1290, 990),
                    new CosmosBilingualText("Modulación + estructura improbable + corrección de errores + repetición → diseño más plausible", "Modulation + improbable structure + error correction + repetition → more plausible design", "Modulaatio + epätodennäköinen rakenne + virheenkorjaus + toisto → uskottavampi suunnittelu", 10000, 9680, 10720),
                    new CosmosBilingualText("Un patrón parece artificial cuando combina regularidades funcionales difíciles de producir por mecanismos naturales conocidos.", "A pattern appears artificial when it combines functional regularities that are difficult to produce by known natural mechanisms.", "Kuvio näyttää keinotekoiselta, kun se yhdistää toiminnallisia säännönmukaisuuksia, joita on vaikea tuottaa tunnetuilla luonnollisilla mekanismeilla.", 8640, 10440, 9720)),
                new RawZoneSeed(22, 12, "shared-mathematics", new Color(0.5294f, 0.6431f, 1.0000f, 1f), new CosmosZoneSoundRecipe(87.00f, 1704.0f, 0.0180f, 0.0350f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Matemáticas compartidas", "Shared Mathematics", "Jaettu matematiikka", 2240, 1390, 1850),
                    new CosmosBilingualText("Relaciones invariantes + ejemplos construidos → primeros puntos de apoyo", "Invariant relations + constructed examples → first support points", "Invarianttirelaatiot + rakennetut esimerkit → ensimmäiset tukipisteet", 6640, 5840, 6760),
                    new CosmosBilingualText("Las matemáticas podrían proporcionar puntos de referencia comunes, pero no constituyen automáticamente un lenguaje completo.", "Mathematics might provide common reference points, but it does not automatically constitute a complete language.", "Matematiikka saattaa tarjota yhteisiä vertailukohtia, mutta se ei automaattisesti muodosta kokonaista kieltä.", 8800, 8680, 7800)),
                new RawZoneSeed(22, 14, "unknown-language", new Color(0.3333f, 0.9216f, 0.9020f, 1f), new CosmosZoneSoundRecipe(93.50f, 1868.0f, 0.0260f, 0.0690f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Lenguaje desconocido", "Unknown language", "Tuntematon kieli", 1840, 1430, 1370),
                    new CosmosBilingualText("Patrones + contexto + respuestas futuras → gramática tentativa y significado parcial", "Patterns + context + future answers → tentative grammar and partial meaning", "Mallit + konteksti + tulevat vastaukset → alustava kielioppi ja osittainen merkitys", 7880, 7120, 7640),
                    new CosmosBilingualText("Un mensaje puede contener estructura sin parecerse a voz, escritura, imagen ni lenguaje humano.", "A message can contain structure without resembling human voice, writing, image, or language.", "Viesti voi sisältää rakennetta, joka ei muistuta ihmisen ääntä, kirjoitusta, kuvaa tai kieltä.", 7960, 7000, 7880)),
                new RawZoneSeed(22, 19, "light-speed-limit", new Color(0.4314f, 0.6941f, 1.0000f, 1f), new CosmosZoneSoundRecipe(109.75f, 2278.0f, 0.0260f, 0.0520f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Límites de la velocidad de la luz", "Limits imposed by the speed of light", "Valonnopeuden rajoitukset", 2480, 2760, 1860),
                    new CosmosBilingualText("Distancia en años luz × trayecto de ida y vuelta → ritmo mínimo del diálogo", "Distance in light years × round trip → minimum dialogue pace", "Etäisyys valovuosina × edestakainen matka → vuoropuhelun vähimmäistahti", 7840, 6600, 6520),
                    new CosmosBilingualText("Cada mensaje muestra el pasado del emisor; una conversación de ida y vuelta puede durar décadas, siglos o más.", "Each message shows the sender's past; a back-and-forth conversation can last decades, centuries, or more.", "Jokainen viesti näyttää lähettäjän menneisyyden; edestakaisin keskustelu voi kestää vuosikymmeniä, vuosisatoja tai enemmän.", 9280, 8800, 10160)),
                new RawZoneSeed(22, 27, "uncertain-coexistence", new Color(0.9216f, 0.9608f, 1.0000f, 1f), new CosmosZoneSoundRecipe(135.75f, 2934.0f, 0.0180f, 0.0860f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Coexistencia incierta", "Uncertain coexistence", "Epävarma rinnakkaiselo", 2120, 1840, 1870),
                    new CosmosBilingualText("Mensajes + memoria + transformación mutua + desacuerdo → historia cósmica compartida", "Messages + memory + mutual transformation + disagreement → shared cosmic history", "Viestit + muisti + keskinäinen muutos + erimielisyys → jaettu kosminen historia", 8280, 8840, 10000),
                    new CosmosBilingualText("El contacto maduro no sería unidad perfecta, sino la capacidad de reconocer otra perspectiva y sostener una relación incompleta.", "Mature contact would not mean perfect unity, but the ability to recognize another perspective and sustain an incomplete relationship.", "Kypsä yhteys ei merkitsisi täydellistä yhtenäisyyttä vaan kykyä tunnistaa toinen näkökulma ja ylläpitää keskeneräistä suhdetta.", 9480, 11000, 9800)),
                new RawZoneSeed(23, 0, "another-perspective", new Color(0.7451f, 0.8627f, 1.0000f, 1f), new CosmosZoneSoundRecipe(48.00f, 720.0f, 0.0080f, 0.0300f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Otra perspectiva sobre la realidad", "Another perspective on reality", "Toinen näkökulma todellisuuteen", 2760, 2520, 2190),
                    new CosmosBilingualText("Contacto logrado → diferencia conceptual reconocida → nuestra propia visión se vuelve visible", "Contact achieved → conceptual difference recognized → our own vision becomes visible", "Yhteys saavutettu → käsitteellinen ero tunnistettu → oma visiomme tulee näkyviin", 7320, 7520, 7120),
                    new CosmosBilingualText("Reconocer otra inteligencia también significa descubrir que la perspectiva humana es una perspectiva entre otras, no un punto de vista neutral.", "Recognizing other intelligence also means discovering that the human perspective is one perspective among others, not a neutral point of view.", "Muun älykkyyden tunnistaminen tarkoittaa myös sen havaitsemista, että ihmisen näkökulma on yksi näkökulma muiden joukossa, ei neutraali näkökulma.", 11040, 10960, 10680)),
                new RawZoneSeed(23, 8, "alien-perspective", new Color(1.0000f, 0.5294f, 0.6863f, 1f), new CosmosZoneSoundRecipe(76.40f, 1424.0f, 0.0120f, 0.1420f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Perspectiva alienígena", "Extraterrestrial perspective", "Vieraan älykkyyden näkökulma", 2160, 2010, 2150),
                    new CosmosBilingualText("Otro mundo + otra historia evolutiva → saliencias y conceptos inesperados", "Another world + another evolutionary history → unexpected salient features and concepts", "Toinen maailma + toinen evoluutiohistoria → odottamattomat olennaiset piirteet ja käsitteet", 6880, 7720, 8520),
                    new CosmosBilingualText("Una civilización alienígena no sería simplemente humana con otra apariencia; podría construir experiencia y conocimiento desde variables que nunca sentimos.", "An extraterrestrial civilization would not simply be humanity in another form; it could build experience and knowledge from variables we never sense.", "Maan ulkopuolinen sivilisaatio ei olisi vain toisen näköinen ihminen; se voisi rakentaa kokemuksensa ja tietonsa muuttujista, joita me emme koskaan aisti.", 12560, 11600, 11800)),
                new RawZoneSeed(23, 12, "collective-intelligences", new Color(0.4314f, 0.7647f, 1.0000f, 1f), new CosmosZoneSoundRecipe(90.60f, 1776.0f, 0.0280f, 0.0720f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Inteligencias colectivas", "Collective intelligences", "Kollektiiviset älykkyydet", 2120, 2030, 1890),
                    new CosmosBilingualText("Agentes locales + señales + retroalimentación → decisión global", "Local agents + signals + feedback → global decision", "Paikalliset toimijat + signaalit + palaute → kokonaisuuden päätös", 7080, 5280, 6680),
                    new CosmosBilingualText("Una colectividad puede resolver problemas que ningún miembro representa por completo, aunque también puede amplificar errores y conflictos.", "A collective can solve problems that no member fully represents, although it can also amplify errors and conflicts.", "Ryhmä voi ratkaista ongelmia, joita yksikään jäsen ei täysin edusta, vaikka se voi myös vahvistaa virheitä ja ristiriitoja.", 9280, 9160, 8920)),
                new RawZoneSeed(23, 17, "multiple-mathematics", new Color(1.0000f, 0.7451f, 0.4706f, 1f), new CosmosZoneSoundRecipe(108.35f, 2216.0f, 0.0200f, 0.1420f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Múltiples matemáticas representadas", "Multiple representations of mathematics", "Matematiikan monet esitystavat", 3120, 3280, 2720),
                    new CosmosBilingualText("Patrones compartidos → notaciones y axiomas diferentes → predicciones comparables", "Shared patterns → different notations and axioms → comparable predictions", "Jaetut kuviot → erilaiset merkinnät ja aksioomit → vertailukelpoiset ennusteet", 6720, 5440, 7760),
                    new CosmosBilingualText("Las relaciones matemáticas pueden ser independientes de una cultura, mientras sus representaciones, prioridades y demostraciones varían profundamente.", "Mathematical relations can be independent of a culture, while their representations, priorities and demonstrations vary profoundly.", "Matemaattiset suhteet voivat olla kulttuurista riippumattomia, vaikka niiden esitystavat, painotukset ja todistukset vaihtelevat suuresti.", 12400, 9120, 10280)),
                new RawZoneSeed(23, 24, "plural-cosmic-perspective", new Color(0.9412f, 0.9608f, 1.0000f, 1f), new CosmosZoneSoundRecipe(133.20f, 2832.0f, 0.0200f, 0.1140f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Perspectiva cósmica plural", "Plural cosmic perspective", "Moninainen kosminen näkökulma", 2520, 1890, 2810),
                    new CosmosBilingualText("Perspectivas limitadas + evidencia compartida + crítica mutua → conocimiento transformable", "Limited perspectives + shared evidence + mutual criticism → transformable knowledge", "Rajoitetut näkökulmat + yhteinen todiste + molemminpuolinen kritiikki → muunnettavissa oleva tieto", 8680, 7920, 8280),
                    new CosmosBilingualText("Comprender el universo puede consistir en integrar perspectivas parciales capaces de corregirse, ampliarse y cambiarse mutuamente sin perder el criterio de evidencia.", "Understanding the universe can consist of integrating partial perspectives capable of correcting, expanding and changing each other without losing the criterion of evidence.", "Universumin ymmärtäminen voi koostua osittaisten näkökulmien yhdistämisestä, jotka pystyvät korjaamaan, laajentamaan ja muuttamaan toisiaan menettämättä todisteiden kriteeriä.", 13720, 13520, 13040)),
                new RawZoneSeed(24, 0, "plural-perspective", new Color(0.7451f, 0.8627f, 1.0000f, 1f), new CosmosZoneSoundRecipe(46.20f, 720.0f, 0.0080f, 0.0280f, CosmosWaveform.Sine),
                    new CosmosBilingualText("Perspectiva cósmica plural", "Plural cosmic perspective", "Moninainen kosminen näkökulma", 2520, 1850, 2280),
                    new CosmosBilingualText("Perspectiva situada + experiencia + historia + relación → algo puede importar de maneras diferentes", "Situated perspective + experience + history + relationship → something can matter in different ways", "Situoitu perspektiivi + kokemus + historia + suhde → jollakin voi olla merkitystä eri tavoin", 10400, 9720, 11480),
                    new CosmosBilingualText("Ninguna inteligencia observa desde un punto completamente neutral; cada forma de comprender también selecciona aquello que considera relevante.", "No intelligence observes from a completely neutral point; Each way of understanding also selects what it considers relevant.", "Mikään älykkyys ei tarkkaile täysin neutraalista pisteestä; Jokainen ymmärrystapa valitsee myös sen, mitä se pitää merkityksellisenä.", 9760, 10120, 9400)),
                new RawZoneSeed(24, 13, "artificial-purposes", new Color(0.3529f, 0.9020f, 0.8627f, 1f), new CosmosZoneSoundRecipe(89.75f, 1838.0f, 0.0120f, 0.1120f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Propósitos artificiales", "artificial purposes", "keinotekoisiin tarkoituksiin", 2520, 1500, 1150),
                    new CosmosBilingualText("Objetivo inicial + aprendizaje + autorreflexión + modificación → propósito posible", "Initial objective + learning + self-reflection + modification → possible purpose", "Alkutavoite + oppiminen + itsereflektio + muutos → mahdollinen tarkoitus", 9720, 7680, 7880),
                    new CosmosBilingualText("Que una meta tenga origen externo no resuelve si puede convertirse en un compromiso propio; los propósitos humanos también nacen de condiciones no elegidas.", "That a goal has an external origin does not resolve whether it can become one's own commitment; Human purposes also arise from unchosen conditions.", "Se, että tavoitteella on ulkoinen alkuperä, ei ratkaise sitä, voiko siitä tulla oma sitoutuminen; Ihmisten tarkoitukset syntyvät myös valitsemattomista olosuhteista.", 11200, 13120, 12200)),
                new RawZoneSeed(24, 19, "beauty-contemplation", new Color(0.9020f, 0.5686f, 0.8627f, 1f), new CosmosZoneSoundRecipe(109.85f, 2354.0f, 0.0120f, 0.0980f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Belleza y contemplación", "Beauty and contemplation", "Kauneutta ja mietiskelyä", 2240, 1940, 2190),
                    new CosmosBilingualText("Patrón + percepción + atención + resonancia → experiencia estética", "Pattern + perception + attention + resonance → aesthetic experience", "Kuvio + havainto + huomio + resonanssi → esteettinen kokemus", 7040, 8800, 8640),
                    new CosmosBilingualText("Una inteligencia puede valorar no solo lo que permite sobrevivir, sino también aquello que considera digno de ser experimentado.", "An intelligence can value not only what allows it to survive, but also what it considers worthy of being experienced.", "Älykkyys voi arvostaa paitsi sitä, mikä mahdollistaa sen selviytymisen, myös sitä, mitä se pitää kokemisen arvoisena.", 9440, 9280, 9480)),
                new RawZoneSeed(24, 24, "self-valuing-universe", new Color(0.8627f, 0.8824f, 0.5686f, 1f), new CosmosZoneSoundRecipe(126.60f, 2784.0f, 0.0080f, 0.0700f, CosmosWaveform.Sine),
                    new CosmosBilingualText("El universo que se valora a sí mismo", "The universe that values itself", "Universumi, joka arvostaa itseään", 2840, 2880, 3200),
                    new CosmosBilingualText("Estrellas → elementos → planetas → vida → experiencia → reflexión → propósito", "Stars → elements → planets → life → experience → reflection → purpose", "Tähdet → elementit → planeetat → elämä → kokemus → heijastus → tarkoitus", 10120, 10160, 10000),
                    new CosmosBilingualText("No es necesario imaginar un universo consciente como totalidad: basta reconocer procesos locales donde el universo se representa y evalúa dentro de sí.", "It is not necessary to imagine a conscious universe as a totality: it is enough to recognize local processes where the universe is represented and evaluated within itself.", "Tietoista maailmankaikkeutta ei tarvitse kuvitella kokonaisuutena: riittää, että tunnistat paikalliset prosessit, joissa maailmankaikkeus on edustettuna ja arvioituna itsessään.", 11200, 14120, 12640)),
                new RawZoneSeed(24, 25, "final-horizon", new Color(0.9608f, 0.9412f, 0.8627f, 1f), new CosmosZoneSoundRecipe(129.95f, 2870.0f, 0.0120f, 0.0840f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Hacia el horizonte final", "Towards the final horizon", "Kohti lopullista horisonttia", 2000, 2560, 2430),
                    new CosmosBilingualText("Propósito sin certeza + responsabilidad compartida + límites de existencia → horizonte final", "Purpose without certainty + shared responsibility + limits of existence → final horizon", "Tarkoitus ilman varmuutta + yhteinen vastuu + olemassaolon rajat → lopullinen horisontti", 9280, 9040, 9200),
                    new CosmosBilingualText("La experiencia termina sin una respuesta única, pero deja visible la posibilidad de construir futuros mejores o peores para quienes los vivirán.", "The experience ends without a single answer, but it leaves visible the possibility of building better or worse futures for those who will live them.", "Kokemus päättyy ilman yhtä vastausta, mutta se jättää näkyväksi mahdollisuuden rakentaa parempaa tai huonompaa tulevaisuutta niille, jotka niitä elävät.", 11040, 11080, 11240)),
                new RawZoneSeed(25, 0, "incomplete-history", new Color(0.9647f, 0.9098f, 0.8039f, 1f), new CosmosZoneSoundRecipe(76.00f, 1850.0f, 0.0240f, 0.0900f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Una sola historia incompleta", "A single incomplete story", "Yksi epätäydellinen tarina", 2800, 2520, 2010),
                    new CosmosBilingualText("Propósito elegido + capacidad física + incertidumbre → varios futuros realmente posibles", "Chosen purpose + physical ability + uncertainty → several truly possible futures", "Valittu tarkoitus + fyysinen kyky + epävarmuus → useita todella mahdollisia tulevaisuuksia", 9200, 7680, 8800),
                    new CosmosBilingualText("Preservar, expandirse, transformarse, coexistir, retirarse o terminar son trayectorias posibles; ninguna está garantizada ni es inevitable.", "Preserve, expand, transform, coexist, withdraw or terminate are possible trajectories; none are guaranteed or inevitable.", "Säilyttäminen, laajentaminen, muuntaminen, olemassaolo rinnakkain, vetäytyminen tai lopettaminen ovat mahdollisia kehityskulkuja; mikään ei ole taattua tai väistämätöntä.", 13760, 10680, 14080)),
                new RawZoneSeed(25, 7, "cosmic-engineering", new Color(0.7059f, 0.6196f, 0.9255f, 1f), new CosmosZoneSoundRecipe(61.65f, 1451.0f, 0.0190f, 0.0720f, CosmosWaveform.Sawtooth),
                    new CosmosBilingualText("Ingeniería cósmica", "cosmic engineering", "Kosminen insinööritaito", 2000, 1500, 2320),
                    new CosmosBilingualText("Tiempo profundo + automatización + energía estelar → proyectos mayores que planetas", "Deep time + automation + stellar energy → projects bigger than planets", "Syvä aika + automaatio + tähtien energia → projektit suurempia kuin planeetat", 8320, 7800, 8320),
                    new CosmosBilingualText("Una tecnología suficientemente avanzada puede transformar estrellas y hábitats, pero no convierte todas las ideas imaginables en posibilidades físicas.", "Sufficiently advanced technology can transform stars and habitats, but it doesn't turn every imaginable idea into physical possibilities.", "Riittävän kehittynyt tekniikka voi muuttaa tähtiä ja elinympäristöjä, mutta se ei muuta jokaista kuviteltavissa olevaa ideaa fyysisiksi mahdollisuuksiksi.", 11320, 10880, 10880)),
                new RawZoneSeed(25, 12, "intelligence-limits", new Color(0.5176f, 0.6471f, 0.9098f, 1f), new CosmosZoneSoundRecipe(51.40f, 1166.0f, 0.0160f, 0.0590f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("Los límites de la inteligencia", "The limits of intelligence", "Älykkyyden rajat", 2320, 2360, 1420),
                    new CosmosBilingualText("Memoria finita + energía finita + tiempo finito + complejidad → problemas resistentes o insolubles", "Finite memory + finite energy + finite time + complexity → resistant or insoluble problems", "Rajallinen muisti + äärellinen energia + äärellinen aika + monimutkaisuus → kestävät tai ratkaisemattomat ongelmat", 11480, 11200, 11560),
                    new CosmosBilingualText("Una civilización puede ser inmensamente capaz y seguir encontrando preguntas que no puede responder eficientemente, o en absoluto.", "A civilization can be immensely capable and still encounter questions that it cannot answer efficiently, or at all.", "Sivilisaatio voi olla äärimmäisen kykenevä ja silti kohdata kysymyksiä, joihin se ei voi vastata tehokkaasti tai ei ollenkaan.", 10760, 8600, 10160)),
                new RawZoneSeed(25, 14, "cosmological-horizon", new Color(0.3765f, 0.7216f, 0.8392f, 1f), new CosmosZoneSoundRecipe(47.30f, 1052.0f, 0.0150f, 0.0540f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("El horizonte cosmológico", "The cosmological horizon", "Kosmologinen horisontti", 2280, 2160, 1880),
                    new CosmosBilingualText("Expansión acelerada + velocidad finita de la luz → regiones que dejan de ser causalmente alcanzables", "Accelerated expansion + finite speed of light → regions that are no longer causally reachable", "Nopeutettu laajeneminen + rajallinen valon nopeus → alueet, jotka eivät ole enää kausaalisesti saavutettavissa", 9600, 8520, 9280),
                    new CosmosBilingualText("El universo disponible para observación puede disminuir incluso mientras el conocimiento y la tecnología aumentan localmente.", "The universe available for observation may decrease even as knowledge and technology increase locally.", "Havainnoitavissa oleva universumi voi pienentyä, vaikka tietämys ja teknologia lisääntyvät paikallisesti.", 7880, 8280, 8600)),
                new RawZoneSeed(25, 24, "final-horizon", new Color(0.9098f, 0.8941f, 0.8392f, 1f), new CosmosZoneSoundRecipe(26.80f, 482.0f, 0.0080f, 0.0280f, CosmosWaveform.Triangle),
                    new CosmosBilingualText("No vemos más allá del horizonte final.", "We do not see beyond the final horizon.", "Emme näe lopullisen horisontin pidemmälle.", 2960, 3800, 3280),
                    new CosmosBilingualText("Evidencia disponible → afirmación responsable | más allá → posibilidad, especulación o inconcebible", "Available evidence → responsible claim | beyond → possibility, speculation or inconceivable", "Käytettävissä olevat todisteet → vastuullinen vaatimus | yli → mahdollisuus, spekulaatio tai käsittämätön", 8600, 8720, 8480),
                    new CosmosBilingualText("Esto no demuestra que no exista nada más. Solo marca el límite entre aquello que podemos describir con evidencia y aquello que todavía pertenece a la posibilidad, la especulación o lo inconcebible.", "This does not prove that nothing else exists. It only marks the limit between what we can describe with evidence and what still belongs to possibility, speculation or the inconceivable.", "Tämä ei todista, etteikö muuta ole olemassa. Se merkitsee vain rajaa sen välillä, mitä voimme kuvata todisteilla ja mikä vielä kuuluu mahdollisuuteen, spekulaatioon tai käsittämättömään.", 14800, 14840, 13600)),
            };

            int totalWords = 0;
            for (int i = 0; i < rawZones.Length; i++)
            {
                rawZones[i].AccumulateWordCounts(ref totalWords);
            }

            WordCues = new CosmosWordCue[totalWords];
            FlagshipZones = new CosmosZoneEntry[rawZones.Length];
            int cueCursor = 0;
            for (int i = 0; i < rawZones.Length; i++)
            {
                FlagshipZones[i] = rawZones[i].Bake(WordCues, ref cueCursor);
            }

            Experiences = new CosmosExperienceMeta[]
            {
                new CosmosExperienceMeta(1, "01_colors", 2, CosmosManifoldArchetype.VolumetricChromaticPrism, "Colores", "Colors", "Värit", "Espectro de colores", "Spectrum of Colors", "Värien kirjo", 0.5, 3.5, 2.0, 1.80f, 1, 3, new Color(1.35f, 0.45f, 1.45f, 1f), new CosmosZoneSoundRecipe(220.00f, 4200.0f, 0.0100f, 0.1800f, CosmosWaveform.Sine), 0, 0),
                new CosmosExperienceMeta(2, "02_music", 3, CosmosManifoldArchetype.CymaticHarmonicHarp, "Música", "Music", "Musiikki", "Espectro musical", "Musical Spectrum", "Musiikin kirjo", 0.5, 3.5, 2.0, 1.90f, 1, 4, new Color(0.45f, 1.25f, 1.55f, 1f), new CosmosZoneSoundRecipe(261.63f, 5200.0f, 0.0100f, 0.2500f, CosmosWaveform.Sine), 0, 0),
                new CosmosExperienceMeta(3, "03_deep_earth", 4, CosmosManifoldArchetype.PlanetaryCutaway, "Tierra profunda", "Deep Earth", "Syvä Maa", "Espectro de la Tierra", "Spectrum of Earth", "Maan kirjo", 0.0, 7.4, 6.8, 2.80f, 14, 0, new Color(0.24f, 0.78f, 1.38f, 1f), new CosmosZoneSoundRecipe(94.00f, 1250.0f, 0.1800f, 0.1600f, CosmosWaveform.Triangle), 0, 6),
                new CosmosExperienceMeta(4, "04_solar_system", 5, CosmosManifoldArchetype.HeliocentricSystem, "Sistema solar", "Solar System", "Aurinkokunta", "Espectro del Sistema Solar · Del Sol a Plutón", "Solar System · Sun to Pluto", "Aurinkokunta · Auringosta Plutoon", 6.8, 13.6, 11.2, 3.20f, 12, 1, new Color(1.45f, 0.92f, 0.34f, 1f), new CosmosZoneSoundRecipe(72.00f, 980.0f, 0.2200f, 0.3100f, CosmosWaveform.Sawtooth), 6, 12),
                new CosmosExperienceMeta(5, "05_milky_way", 6, CosmosManifoldArchetype.SpiralGalaxy, "Vía Láctea", "Milky Way", "Linnunrata", "Espectro de la Vía Láctea · Del sistema solar al borde galáctico", "Milky Way Spectrum", "Linnunradan kirjo", 13.6, 21.1, 20.5, 3.50f, 13, 2, new Color(0.55f, 0.85f, 1.45f, 1f), new CosmosZoneSoundRecipe(126.00f, 1900.0f, 0.1300f, 0.1700f, CosmosWaveform.Sine), 18, 8),
                new CosmosExperienceMeta(6, "06_local_group", 8, CosmosManifoldArchetype.SpiralGalaxy, "Grupo Local", "Local Group", "Paikallinen ryhmä", "Espectro del Grupo Local: de la Vía Láctea a Andrómeda", "Local Group Spectrum", "Paikallisen ryhmän kirjo", 21.1, 22.6, 22.0, 3.00f, 9, 2, new Color(0.75f, 0.72f, 1.38f, 1f), new CosmosZoneSoundRecipe(98.00f, 1550.0f, 0.0950f, 0.2500f, CosmosWaveform.Triangle), 26, 5),
                new CosmosExperienceMeta(7, "07_laniakea", 9, CosmosManifoldArchetype.GravitationalFlowBasin, "Laniakea", "Laniakea", "Laniakea", "Espectro de Laniakea: del Grupo Local al Gran Atractor", "Laniakea Basin Spectrum", "Laniakean kirjo", 22.6, 24.5, 23.8, 3.00f, 10, 2, new Color(1.28f, 0.78f, 0.42f, 1f), new CosmosZoneSoundRecipe(132.00f, 2600.0f, 0.0520f, 0.1450f, CosmosWaveform.Sine), 31, 6),
                new CosmosExperienceMeta(8, "08_cosmic_web", 10, CosmosManifoldArchetype.VoronoiCosmicWeb, "Red cósmica", "Cosmic Web", "Kosminen verkko", "Espectro de la Red Cósmica: de Laniakea a la Estructura del Universo", "Cosmic Web Spectrum", "Kosmisen verkon kirjo", 24.5, 26.0, 25.3, 3.20f, 11, 2, new Color(0.85f, 0.58f, 1.65f, 1f), new CosmosZoneSoundRecipe(98.00f, 1480.0f, 0.0550f, 0.2100f, CosmosWaveform.Triangle), 37, 5),
                new CosmosExperienceMeta(9, "09_observable_universe", 11, CosmosManifoldArchetype.DeepFieldSphericalShell, "Universo observable", "Observable Universe", "Havaittava maailmankaikkeus", "Espectro del Universo Observable: de la Red Cósmica al Horizonte Cósmico", "Observable Universe Spectrum", "Havaittavan maailmankaikkeuden kirjo", 26.0, 26.6, 26.4, 3.00f, 11, 2, new Color(1.15f, 0.75f, 0.52f, 1f), new CosmosZoneSoundRecipe(132.00f, 2860.0f, 0.0520f, 0.1200f, CosmosWaveform.Triangle), 42, 5),
                new CosmosExperienceMeta(10, "10_early_universe", 12, CosmosManifoldArchetype.DeepFieldSphericalShell, "Universo temprano", "Early Universe", "Varhainen maailmankaikkeus", "Espectro del Universo Temprano: del Fondo Cósmico a la Inflación", "Early Universe Spectrum", "Varhaisen maailmankaikkeuden kirjo", 26.6, 27.2, 26.9, 2.80f, 9, 2, new Color(1.55f, 0.58f, 0.35f, 1f), new CosmosZoneSoundRecipe(118.00f, 2300.0f, 0.1200f, 0.1400f, CosmosWaveform.Sine), 47, 5),
                new CosmosExperienceMeta(11, "11_cosmic_evidence", 13, CosmosManifoldArchetype.HyperHorizonLotusWing, "Evidencia cósmica", "Cosmic Evidence", "Kosminen todistusaineisto", "Espectro de la Evidencia Cósmica: de las Huellas Observables a las Preguntas del Origen", "Cosmic Evidence Spectrum", "Kosmisen todistusaineiston kirjo", 27.2, 28.6, 27.6, 2.20f, 9, 5, new Color(0.48f, 1.32f, 1.15f, 1f), new CosmosZoneSoundRecipe(154.00f, 3100.0f, 0.0950f, 0.0850f, CosmosWaveform.Triangle), 52, 4),
                new CosmosExperienceMeta(12, "12_future_of_universe", 14, CosmosManifoldArchetype.HyperHorizonLotusWing, "Futuro del universo", "Future of Universe", "Maailmankaikkeuden tulevaisuus", "Espectro del Futuro del Universo: de la Era Estelar a la Oscuridad Cósmica", "Future of the Universe", "Maailmankaikkeuden tulevaisuus", 27.2, 28.6, 27.6, 2.20f, 10, 5, new Color(0.58f, 0.42f, 1.35f, 1f), new CosmosZoneSoundRecipe(54.00f, 610.0f, 0.0350f, 0.0400f, CosmosWaveform.Sine), 56, 4),
                new CosmosExperienceMeta(13, "13_matter_energy", 15, CosmosManifoldArchetype.QuantumSubatomicLattice, "Materia y energía", "Matter & Energy", "Aine ja energia", "Espectro de la Materia y la Energía: de los Átomos a los Campos Cuánticos", "Matter & Energy · Atoms to Fields", "Aine ja energia · Atomeista kenttiin", -22.0, 0.0, -11.0, 2.20f, 12, 3, new Color(0.28f, 1.35f, 1.48f, 1f), new CosmosZoneSoundRecipe(126.00f, 1980.0f, 0.0700f, 0.2200f, CosmosWaveform.Sawtooth), 60, 8),
                new CosmosExperienceMeta(14, "14_spacetime", 16, CosmosManifoldArchetype.QuantumSubatomicLattice, "Espacio-tiempo", "Spacetime", "Aika-avaruus", "Espectro del Espacio-Tiempo: de la Relatividad a la Gravedad Cuántica", "Spacetime · Relativity to Planck", "Aika-avaruus · Suhteellisuudesta Planckiin", -35.0, -22.0, -30.0, 2.20f, 15, 3, new Color(1.22f, 0.48f, 1.62f, 1f), new CosmosZoneSoundRecipe(111.00f, 1980.0f, 0.0650f, 0.2400f, CosmosWaveform.Sawtooth), 68, 6),
                new CosmosExperienceMeta(15, "15_complexity", 17, CosmosManifoldArchetype.EmergenceHelix, "Complejidad", "Complexity", "Monimutkaisuus", "Espectro de la Complejidad: de las Leyes Simples al Umbral de la Vida", "Complexity & Emergence", "Monimutkaisuus ja emergenssi", -9.0, -5.0, -7.0, 2.20f, 16, 4, new Color(0.28f, 1.42f, 0.92f, 1f), new CosmosZoneSoundRecipe(103.00f, 1980.0f, 0.0520f, 0.2800f, CosmosWaveform.Sine), 74, 5),
                new CosmosExperienceMeta(16, "16_life", 18, CosmosManifoldArchetype.EmergenceHelix, "Vida", "Life", "Elämä", "Espectro de la Vida: de la Primera Célula a la Conciencia", "Spectrum of Life", "Elämän kirjo", -6.0, 2.0, -1.0, 2.40f, 19, 4, new Color(0.32f, 1.38f, 0.65f, 1f), new CosmosZoneSoundRecipe(129.00f, 2460.0f, 0.0400f, 0.4700f, CosmosWaveform.Sine), 79, 7),
                new CosmosExperienceMeta(17, "17_mind", 19, CosmosManifoldArchetype.ConnectomeCathedral, "Mente", "Mind", "Mieli", "Espectro de la Mente: de la Percepción al Pensamiento", "Spectrum of Mind", "Mielen kirjo", 1.5, 4.5, 2.8, 2.40f, 18, 4, new Color(1.35f, 0.65f, 1.45f, 1f), new CosmosZoneSoundRecipe(156.00f, 3100.0f, 0.0150f, 0.6700f, CosmosWaveform.Sine), 86, 6),
                new CosmosExperienceMeta(18, "18_culture", 20, CosmosManifoldArchetype.OrbitalCivilizationWeb, "Cultura", "Culture", "Kulttuuri", "Espectro de la Cultura: del Conocimiento Compartido a la Civilización", "Spectrum of Culture", "Kulttuurin kirjo", 4.5, 6.9, 6.2, 2.20f, 20, 5, new Color(1.48f, 1.05f, 0.42f, 1f), new CosmosZoneSoundRecipe(99.00f, 1810.0f, 0.0180f, 0.1750f, CosmosWaveform.Sine), 92, 6),
                new CosmosExperienceMeta(19, "19_technology", 21, CosmosManifoldArchetype.OrbitalCivilizationWeb, "Tecnología", "Technology", "Teknologia", "Espectro de la Tecnología: de las Herramientas a la Inteligencia Artificial", "Spectrum of Technology", "Teknologian kirjo", 6.9, 8.2, 7.6, 2.20f, 20, 5, new Color(0.42f, 1.18f, 1.68f, 1f), new CosmosZoneSoundRecipe(100.50f, 1840.0f, 0.0170f, 0.1270f, CosmosWaveform.Sine), 98, 6),
                new CosmosExperienceMeta(20, "20_futures", 22, CosmosManifoldArchetype.OrbitalCivilizationWeb, "Futuros", "Futures", "Tulevaisuudet", "Espectro de los Futuros: de la Civilización Planetaria al Destino Cósmico", "Spectrum of Futures", "Tulevaisuuksien kirjo", 8.2, 16.0, 11.8, 2.20f, 19, 5, new Color(1.12f, 1.32f, 0.52f, 1f), new CosmosZoneSoundRecipe(94.75f, 1762.0f, 0.0340f, 0.0920f, CosmosWaveform.Triangle), 104, 6),
                new CosmosExperienceMeta(21, "21_cosmic_life", 7, CosmosManifoldArchetype.ExoplanetBiosphereBelt, "Vida cósmica", "Cosmic Life", "Kosminen elämä", "Espectro de la Vida Cósmica: del Planeta Habitable a la Inteligencia Extraterrestre", "Cosmic Life Spectrum", "Kosmisen elämän kirjo", 15.5, 20.0, 18.0, 2.40f, 25, 5, new Color(0.38f, 1.45f, 1.12f, 1f), new CosmosZoneSoundRecipe(91.80f, 1776.0f, 0.0200f, 0.1300f, CosmosWaveform.Sine), 110, 6),
                new CosmosExperienceMeta(22, "22_cosmic_contact", 7, CosmosManifoldArchetype.ExoplanetBiosphereBelt, "Contacto cósmico", "Cosmic Contact", "Kosminen yhteys", "Espectro del Contacto Cósmico: de la Primera Señal al Diálogo entre Civilizaciones", "Cosmic Contact & SETI", "Kosminen yhteys & SETI", 16.0, 20.8, 18.6, 2.50f, 28, 5, new Color(1.55f, 0.82f, 0.48f, 1f), new CosmosZoneSoundRecipe(93.50f, 1868.0f, 0.0260f, 0.0690f, CosmosWaveform.Sine), 116, 6),
                new CosmosExperienceMeta(23, "23_cosmic_perspectives", 23, CosmosManifoldArchetype.HyperHorizonLotusWing, "Perspectivas cósmicas", "Cosmic Perspectives", "Kosmiset näkökulmat", "Espectro de las Perspectivas Cósmicas: de una Visión Humana a Muchas Formas de Comprender el Universo", "Cosmic Perspectives", "Kosmiset näkökulmat", 27.2, 28.6, 27.6, 2.20f, 25, 5, new Color(0.95f, 0.85f, 1.55f, 1f), new CosmosZoneSoundRecipe(90.60f, 1776.0f, 0.0280f, 0.0720f, CosmosWaveform.Sine), 122, 5),
                new CosmosExperienceMeta(24, "24_cosmic_meaning", 24, CosmosManifoldArchetype.HyperHorizonLotusWing, "Significado cósmico", "Cosmic Meaning", "Kosminen merkitys", "Espectro del Significado Cósmico: de un Universo Indiferente a la Construcción de Propósito", "Cosmic Meaning", "Kosminen merkitys", 27.2, 28.6, 27.6, 2.20f, 26, 5, new Color(1.42f, 0.72f, 1.28f, 1f), new CosmosZoneSoundRecipe(89.75f, 1838.0f, 0.0120f, 0.1120f, CosmosWaveform.Triangle), 127, 5),
                new CosmosExperienceMeta(25, "25_final_horizon", 25, CosmosManifoldArchetype.HyperHorizonLotusWing, "Horizonte final", "Final Horizon", "Lopullinen horisontti", "Espectro del Horizonte Final: del Propósito Cósmico a los Límites de la Existencia", "Final Horizon Spectrum", "Lopullisen horisontin kirjo", 27.5, 29.0, 28.2, 2.80f, 25, 5, new Color(1.45f, 1.25f, 0.88f, 1f), new CosmosZoneSoundRecipe(51.40f, 1166.0f, 0.0160f, 0.0590f, CosmosWaveform.Triangle), 132, 5),
            };

            ContinuityEdges = new CosmosContinuityEdge[]
            {
                new CosmosContinuityEdge(3, 13, CosmosBridgeKind.ScaleDiveInward),
                new CosmosContinuityEdge(13, 14, CosmosBridgeKind.ScaleDiveInward),
                new CosmosContinuityEdge(14, 15, CosmosBridgeKind.EmergenceHelix),
                new CosmosContinuityEdge(15, 16, CosmosBridgeKind.EmergenceHelix),
                new CosmosContinuityEdge(16, 17, CosmosBridgeKind.EmergenceHelix),
                new CosmosContinuityEdge(17, 1, CosmosBridgeKind.NestedSensoryLobe),
                new CosmosContinuityEdge(17, 2, CosmosBridgeKind.NestedSensoryLobe),
                new CosmosContinuityEdge(17, 18, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(18, 19, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(19, 20, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(3, 4, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(4, 5, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(5, 21, CosmosBridgeKind.EmbeddedStellarArm),
                new CosmosContinuityEdge(21, 22, CosmosBridgeKind.EmbeddedStellarArm),
                new CosmosContinuityEdge(5, 6, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(6, 7, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(7, 8, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(8, 9, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(9, 10, CosmosBridgeKind.ScaleRiseOutward),
                new CosmosContinuityEdge(10, 11, CosmosBridgeKind.HyperHorizonLotusPetal),
                new CosmosContinuityEdge(10, 12, CosmosBridgeKind.HyperHorizonLotusPetal),
                new CosmosContinuityEdge(10, 23, CosmosBridgeKind.HyperHorizonLotusPetal),
                new CosmosContinuityEdge(10, 24, CosmosBridgeKind.HyperHorizonLotusPetal),
                new CosmosContinuityEdge(10, 25, CosmosBridgeKind.HyperHorizonLotusPetal),
                new CosmosContinuityEdge(12, 13, CosmosBridgeKind.OuroborosReturn),
            };

            SolarBodies = new CosmosSolarBodyMeta[]
            {
                new CosmosSolarBodyMeta("sun", 0, 0.025f, 0.000f, 92.0f, new Color(1.45f, 0.92f, 0.30f, 1f), new Color(1.55f, 0.56f, 0.14f, 1f), false),
                new CosmosSolarBodyMeta("mercury", 1, 0.130f, -0.120f, 8.0f, new Color(0.58f, 0.54f, 0.50f, 1f), new Color(0.40f, 0.36f, 0.34f, 1f), false),
                new CosmosSolarBodyMeta("venus", 2, 0.210f, 0.130f, 18.0f, new Color(0.92f, 0.76f, 0.48f, 1f), new Color(0.78f, 0.56f, 0.28f, 1f), false),
                new CosmosSolarBodyMeta("earth", 3, 0.295f, -0.080f, 21.0f, new Color(0.22f, 0.56f, 0.92f, 1f), new Color(0.30f, 0.72f, 0.44f, 1f), false),
                new CosmosSolarBodyMeta("mars", 4, 0.385f, 0.150f, 14.0f, new Color(0.84f, 0.34f, 0.20f, 1f), new Color(0.60f, 0.22f, 0.12f, 1f), false),
                new CosmosSolarBodyMeta("ceres", 5, 0.475f, -0.130f, 5.0f, new Color(0.54f, 0.52f, 0.50f, 1f), new Color(0.42f, 0.42f, 0.44f, 1f), false),
                new CosmosSolarBodyMeta("jupiter", 6, 0.575f, 0.050f, 44.0f, new Color(0.88f, 0.68f, 0.46f, 1f), new Color(0.68f, 0.40f, 0.26f, 1f), false),
                new CosmosSolarBodyMeta("saturn", 7, 0.680f, -0.110f, 32.0f, new Color(0.90f, 0.80f, 0.56f, 1f), new Color(0.74f, 0.62f, 0.42f, 1f), true),
                new CosmosSolarBodyMeta("uranus", 8, 0.775f, 0.150f, 23.0f, new Color(0.52f, 0.86f, 0.92f, 1f), new Color(0.38f, 0.72f, 0.82f, 1f), true),
                new CosmosSolarBodyMeta("neptune", 9, 0.860f, -0.140f, 23.0f, new Color(0.24f, 0.44f, 0.96f, 1f), new Color(0.38f, 0.60f, 1.00f, 1f), false),
                new CosmosSolarBodyMeta("pluto", 11, 0.980f, 0.075f, 12.0f, new Color(0.78f, 0.70f, 0.62f, 1f), new Color(0.52f, 0.46f, 0.42f, 1f), false),
            };

            GalacticLandmarks = new CosmosGalacticLandmarkMeta[]
            {
                new CosmosGalacticLandmarkMeta(5, "sun", 0, 0.048f, -0.100f, 20.0f),
                new CosmosGalacticLandmarkMeta(5, "heliosphere", 1, 0.122f, 0.080f, 58.0f),
                new CosmosGalacticLandmarkMeta(5, "oort", 2, 0.218f, -0.050f, 72.0f),
                new CosmosGalacticLandmarkMeta(5, "interstellar", 3, 0.305f, 0.120f, 52.0f),
                new CosmosGalacticLandmarkMeta(5, "alpha", 4, 0.392f, -0.100f, 29.0f),
                new CosmosGalacticLandmarkMeta(5, "neighborhood", 5, 0.482f, 0.130f, 62.0f),
                new CosmosGalacticLandmarkMeta(5, "bubble", 6, 0.578f, -0.070f, 78.0f),
                new CosmosGalacticLandmarkMeta(5, "orion", 7, 0.666f, 0.080f, 84.0f),
                new CosmosGalacticLandmarkMeta(5, "nebula", 8, 0.748f, -0.120f, 54.0f),
                new CosmosGalacticLandmarkMeta(5, "disk", 9, 0.832f, 0.080f, 94.0f),
                new CosmosGalacticLandmarkMeta(5, "center", 10, 0.909f, -0.060f, 40.0f),
                new CosmosGalacticLandmarkMeta(5, "halo", 11, 0.963f, 0.040f, 84.0f),
                new CosmosGalacticLandmarkMeta(6, "milky-edge", 0, 0.035f, -0.040f, 170.0f),
                new CosmosGalacticLandmarkMeta(6, "outer-halo", 1, 0.125f, 0.050f, 118.0f),
                new CosmosGalacticLandmarkMeta(6, "dwarfs", 2, 0.240f, -0.030f, 84.0f),
                new CosmosGalacticLandmarkMeta(6, "intergalactic", 3, 0.360f, 0.060f, 104.0f),
                new CosmosGalacticLandmarkMeta(6, "magellanic", 4, 0.490f, -0.070f, 94.0f),
                new CosmosGalacticLandmarkMeta(6, "triangulum", 5, 0.605f, 0.150f, 68.0f),
                new CosmosGalacticLandmarkMeta(6, "andromeda", 6, 0.745f, -0.020f, 168.0f),
                new CosmosGalacticLandmarkMeta(6, "center", 7, 0.868f, 0.090f, 86.0f),
                new CosmosGalacticLandmarkMeta(6, "boundary", 8, 0.965f, 0.000f, 150.0f),
                new CosmosGalacticLandmarkMeta(7, "local-group-limit", 0, 0.032f, -0.020f, 150.0f),
                new CosmosGalacticLandmarkMeta(7, "local-volume", 1, 0.120f, 0.040f, 104.0f),
                new CosmosGalacticLandmarkMeta(7, "local-sheet", 2, 0.225f, -0.040f, 140.0f),
                new CosmosGalacticLandmarkMeta(7, "virgo-cluster", 3, 0.345f, 0.090f, 112.0f),
                new CosmosGalacticLandmarkMeta(7, "virgo-supercluster", 4, 0.460f, -0.030f, 145.0f),
                new CosmosGalacticLandmarkMeta(7, "galaxy-flows", 5, 0.585f, 0.000f, 160.0f),
                new CosmosGalacticLandmarkMeta(7, "local-void", 6, 0.690f, -0.180f, 118.0f),
                new CosmosGalacticLandmarkMeta(7, "great-attractor", 7, 0.805f, 0.060f, 196.0f),
                new CosmosGalacticLandmarkMeta(7, "laniakea-basin", 8, 0.915f, 0.000f, 230.0f),
                new CosmosGalacticLandmarkMeta(7, "boundary", 9, 0.982f, 0.000f, 180.0f),
            };

            MusicTimbres = new CosmosMusicTimbre[]
            {
                new CosmosMusicTimbre(0, "cristal", "crystal", "kristalli", CosmosWaveform.Sine, 0.25f, 7.0f, 5200.0f, 0.015f, 0.62f),
                new CosmosMusicTimbre(1, "aire", "airy", "ilmava", CosmosWaveform.Triangle, 0.50f, 5.0f, 3400.0f, 0.025f, 0.46f),
                new CosmosMusicTimbre(2, "cálido", "warm", "lämmin", CosmosWaveform.Sawtooth, 0.75f, 3.0f, 1900.0f, 0.035f, 0.38f),
                new CosmosMusicTimbre(3, "profundo", "deep", "syvä", CosmosWaveform.Square, 1.00f, 2.0f, 980.0f, 0.045f, 0.50f),
            };

            MusicPitches = new CosmosMusicPitch[]
            {
                new CosmosMusicPitch(0, "do", "C"),
                new CosmosMusicPitch(1, "do sostenido", "C♯"),
                new CosmosMusicPitch(2, "re", "D"),
                new CosmosMusicPitch(3, "re sostenido", "D♯"),
                new CosmosMusicPitch(4, "mi", "E"),
                new CosmosMusicPitch(5, "fa", "F"),
                new CosmosMusicPitch(6, "fa sostenido", "F♯"),
                new CosmosMusicPitch(7, "sol", "G"),
                new CosmosMusicPitch(8, "sol sostenido", "G♯"),
                new CosmosMusicPitch(9, "la", "A"),
                new CosmosMusicPitch(10, "la sostenido", "A♯"),
                new CosmosMusicPitch(11, "si", "B"),
            };

            ColorSamples = new CosmosColorSample[]
            {
                CreateColorSample(0f, 90f, 52f, "rojo", "red", "punainen"),
                CreateColorSample(28f, 92f, 56f, "naranja", "orange", "oranssi"),
                CreateColorSample(54f, 95f, 58f, "amarillo", "yellow", "keltainen"),
                CreateColorSample(128f, 78f, 46f, "verde", "green", "vihreä"),
                CreateColorSample(170f, 82f, 50f, "turquesa", "turquoise", "turkoosi"),
                CreateColorSample(186f, 92f, 54f, "cian", "cyan", "syaani"),
                CreateColorSample(202f, 85f, 68f, "celeste", "celeste", "celeste"),
                CreateColorSample(224f, 88f, 52f, "azul", "blue", "sininen"),
                CreateColorSample(274f, 82f, 56f, "violeta", "violet", "violetti"),
                CreateColorSample(308f, 88f, 56f, "magenta", "magenta", "magenta"),
                CreateColorSample(338f, 86f, 68f, "rosa", "pink", "vaaleanpunainen"),
                CreateColorSample(44f, 90f, 56f, "dorado", "gold", "kultainen"),
                CreateColorSample(0f, 0f, 96f, "blanco", "white", "valkoinen"),
                CreateColorSample(0f, 0f, 50f, "gris", "gray", "harmaa"),
                CreateColorSample(0f, 0f, 8f, "negro", "black", "musta"),
            };
        }

        public static bool TryGetExperience(byte id, out CosmosExperienceMeta meta)
        {
            if (id < 1 || id > ExperienceCount)
            {
                meta = default;
                return false;
            }

            meta = Experiences[id - 1];
            return true;
        }

        public static CosmosExperienceMeta GetExperience(byte id)
        {
            int idx = id < 1 ? 0 : (id > ExperienceCount ? ExperienceCount - 1 : id - 1);
            return Experiences[idx];
        }

        public static bool TryFindExperienceForScale(double scaleExp, out CosmosExperienceMeta meta)
        {
            int bestIndex = 2; // Deep Earth cold start fallback
            double bestDistance = double.MaxValue;
            for (int i = 0; i < Experiences.Length; i++)
            {
                CosmosExperienceMeta e = Experiences[i];
                if (scaleExp >= e.ScaleExpMin && scaleExp <= e.ScaleExpMax)
                {
                    double dist = Math.Abs(scaleExp - e.FocusScaleExp);
                    if (dist < bestDistance)
                    {
                        bestDistance = dist;
                        bestIndex = i;
                    }
                }
            }

            if (bestDistance < double.MaxValue)
            {
                meta = Experiences[bestIndex];
                return true;
            }

            for (int i = 0; i < Experiences.Length; i++)
            {
                double dist = Math.Abs(scaleExp - Experiences[i].FocusScaleExp);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestIndex = i;
                }
            }

            meta = Experiences[bestIndex];
            return true;
        }

        public static void GetFlagshipZones(byte experienceId, out int startIndex, out int count)
        {
            if (experienceId < 1 || experienceId > ExperienceCount)
            {
                startIndex = 0;
                count = 0;
                return;
            }

            CosmosExperienceMeta meta = Experiences[experienceId - 1];
            startIndex = meta.FlagshipStartIndex;
            count = meta.FlagshipCount;
        }

        public static bool TryGetZone(byte experienceId, byte zoneIndex, out CosmosZoneEntry entry)
        {
            GetFlagshipZones(experienceId, out int start, out int count);
            for (int i = 0; i < count; i++)
            {
                CosmosZoneEntry candidate = FlagshipZones[start + i];
                if (candidate.ZoneIndex == zoneIndex)
                {
                    entry = candidate;
                    return true;
                }
            }

            if (count > 0)
            {
                CosmosZoneEntry closest = FlagshipZones[start];
                int bestDelta = Math.Abs(closest.ZoneIndex - zoneIndex);
                for (int i = 1; i < count; i++)
                {
                    CosmosZoneEntry candidate = FlagshipZones[start + i];
                    int delta = Math.Abs(candidate.ZoneIndex - zoneIndex);
                    if (delta < bestDelta)
                    {
                        bestDelta = delta;
                        closest = candidate;
                    }
                }

                entry = closest;
                return true;
            }

            entry = default;
            return false;
        }

        public static bool TryGetPrimaryZoneForExperience(byte experienceId, out CosmosZoneEntry entry)
        {
            GetFlagshipZones(experienceId, out int start, out int count);
            if (count <= 0)
            {
                entry = default;
                return false;
            }

            entry = FlagshipZones[start + (count >> 1)];
            return true;
        }

        public static void GetWordCues(CosmosCueSpan span, out CosmosWordCue[] buffer, out int offset, out int count)
        {
            buffer = WordCues;
            offset = span.Offset;
            count = span.Count;
        }

        public static float ResolveMidiFrequency(int midiNote)
        {
            return 440f * Mathf.Pow(2f, (midiNote - 69) / 12f);
        }

        public static CosmosMusicPitch GetMusicPitch(int midiNote)
        {
            int mod = ((midiNote % 12) + 12) % 12;
            return MusicPitches[mod];
        }

        public static Vector3 HslToPrismLocal(float hueDeg, float satPct, float lightPct)
        {
            float modHue = ((hueDeg % 360f) + 360f) % 360f;
            float theta = modHue * Mathf.Deg2Rad;
            float s01 = Mathf.Clamp01(satPct * 0.01f);
            float l01 = Mathf.Clamp01(lightPct * 0.01f);
            // Bicone silhouette matches App 01 Colors Chromatic Prism
            float coneScale = 1f - Mathf.Abs(2f * l01 - 1f) * 0.45f;
            float r = (0.15f + 0.85f * s01) * coneScale;
            float y = (l01 - 0.5f) * 1.6f;
            return new Vector3(r * Mathf.Cos(theta), y, r * Mathf.Sin(theta));
        }

        public static Color HslToLinear(float hueDeg, float satPct, float lightPct)
        {
            float h = (((hueDeg % 360f) + 360f) % 360f) / 360f;
            float s = Mathf.Clamp01(satPct * 0.01f);
            float l = Mathf.Clamp01(lightPct * 0.01f);
            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            float r = HueToRgb(p, q, h + 1f / 3f);
            float g = HueToRgb(p, q, h);
            float b = HueToRgb(p, q, h - 1f / 3f);
            return new Color(r, g, b, 1f).linear;
        }

        public static CosmosColorSample ResolveNearestColorSample(float hueDeg, float satPct, float lightPct)
        {
            Color targetLin = HslToLinear(hueDeg, satPct, lightPct);
            int bestIdx = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < ColorSamples.Length; i++)
            {
                Color c = ColorSamples[i].LinearColor;
                float dr = targetLin.r - c.r;
                float dg = targetLin.g - c.g;
                float db = targetLin.b - c.b;
                float d = dr * dr + dg * dg + db * db;
                if (d < bestDist)
                {
                    bestDist = d;
                    bestIdx = i;
                }
            }

            return ColorSamples[bestIdx];
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 0.5f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }

        private static CosmosColorSample CreateColorSample(
            float hueDeg,
            float satPct,
            float lightPct,
            string labelEs,
            string labelEn,
            string labelFi)
        {
            return new CosmosColorSample(
                hueDeg,
                satPct,
                lightPct,
                labelEs,
                labelEn,
                labelFi,
                HslToLinear(hueDeg, satPct, lightPct),
                HslToPrismLocal(hueDeg, satPct, lightPct));
        }

        private readonly struct RawZoneSeed
        {
            public readonly byte ExperienceId;
            public readonly byte ZoneIndex;
            public readonly string ZoneId;
            public readonly Color SrgbColor;
            public readonly CosmosZoneSoundRecipe Sound;
            public readonly CosmosBilingualText Name;
            public readonly CosmosBilingualText Metric;
            public readonly CosmosBilingualText Fact;

            public RawZoneSeed(
                byte experienceId,
                byte zoneIndex,
                string zoneId,
                Color srgbColor,
                CosmosZoneSoundRecipe sound,
                CosmosBilingualText name,
                CosmosBilingualText metric,
                CosmosBilingualText fact)
            {
                ExperienceId = experienceId;
                ZoneIndex = zoneIndex;
                ZoneId = zoneId;
                SrgbColor = srgbColor;
                Sound = sound;
                Name = name;
                Metric = metric;
                Fact = fact;
            }

            public void AccumulateWordCounts(ref int total)
            {
                total += CountWords(Name.Es) + CountWords(Name.En) + CountWords(Name.Fi);
                total += CountWords(Metric.Es) + CountWords(Metric.En) + CountWords(Metric.Fi);
                total += CountWords(Fact.Es) + CountWords(Fact.En) + CountWords(Fact.Fi);
            }

            public CosmosZoneEntry Bake(CosmosWordCue[] dest, ref int cursor)
            {
                CosmosCueSpan nEs = BakeTextCues(Name.Es, Name.DurationMsEs, dest, ref cursor);
                CosmosCueSpan nEn = BakeTextCues(Name.En, Name.DurationMsEn, dest, ref cursor);
                CosmosCueSpan nFi = BakeTextCues(Name.Fi, Name.DurationMsFi, dest, ref cursor);
                CosmosCueSpan mEs = BakeTextCues(Metric.Es, Metric.DurationMsEs, dest, ref cursor);
                CosmosCueSpan mEn = BakeTextCues(Metric.En, Metric.DurationMsEn, dest, ref cursor);
                CosmosCueSpan mFi = BakeTextCues(Metric.Fi, Metric.DurationMsFi, dest, ref cursor);
                CosmosCueSpan fEs = BakeTextCues(Fact.Es, Fact.DurationMsEs, dest, ref cursor);
                CosmosCueSpan fEn = BakeTextCues(Fact.En, Fact.DurationMsEn, dest, ref cursor);
                CosmosCueSpan fFi = BakeTextCues(Fact.Fi, Fact.DurationMsFi, dest, ref cursor);

                return new CosmosZoneEntry(
                    ExperienceId,
                    ZoneIndex,
                    ZoneId,
                    SrgbColor,
                    Sound,
                    Name,
                    Metric,
                    Fact,
                    nEs, nEn, nFi,
                    mEs, mEn, mFi,
                    fEs, fEn, fFi);
            }
        }

        private static int CountWords(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            int count = 0;
            bool inWord = false;
            for (int i = 0; i < text.Length; i++)
            {
                bool ws = char.IsWhiteSpace(text[i]);
                if (!ws && !inWord)
                {
                    count++;
                    inWord = true;
                }
                else if (ws)
                {
                    inWord = false;
                }
            }

            return count;
        }

        private static CosmosCueSpan BakeTextCues(string text, ushort durationMs, CosmosWordCue[] dest, ref int cursor)
        {
            if (string.IsNullOrEmpty(text) || dest == null)
            {
                return new CosmosCueSpan(cursor, 0);
            }

            int startCursor = cursor;
            float totalWeight = 0f;
            int len = text.Length;

            // Pass 1: total SyncVoice acoustic duration weight across tokens
            int i = 0;
            while (i < len)
            {
                while (i < len && char.IsWhiteSpace(text[i])) i++;
                if (i >= len) break;
                int ws = i;
                while (i < len && !char.IsWhiteSpace(text[i])) i++;
                totalWeight += ComputeWordWeight(text, ws, i - ws);
            }

            if (totalWeight <= 0f)
            {
                return new CosmosCueSpan(startCursor, 0);
            }

            // Pass 2: emit exact non-allocating word spans calibrated against catalog duration_ms
            float accum = 0f;
            float totalMs = durationMs > 0 ? durationMs : 1200f;
            i = 0;
            while (i < len)
            {
                while (i < len && char.IsWhiteSpace(text[i])) i++;
                if (i >= len) break;
                int ws = i;
                while (i < len && !char.IsWhiteSpace(text[i])) i++;
                int wlen = i - ws;
                float weight = ComputeWordWeight(text, ws, wlen);
                float normStart = accum / totalWeight;
                accum += weight;
                float normEnd = accum / totalWeight;
                uint startMs = (uint)Mathf.RoundToInt(normStart * totalMs);
                uint endMs = (uint)Mathf.RoundToInt(normEnd * totalMs);
                dest[cursor++] = new CosmosWordCue(
                    (ushort)ws,
                    (ushort)wlen,
                    normStart,
                    normEnd,
                    startMs,
                    endMs);
            }

            return new CosmosCueSpan(startCursor, cursor - startCursor);
        }

        private static float ComputeWordWeight(string text, int start, int length)
        {
            float baseWeight = 1.2f + length * 0.45f;
            char tail = text[start + length - 1];
            if (tail == '.' || tail == '!' || tail == '?')
            {
                baseWeight += 2.2f;
            }
            else if (tail == ',' || tail == ';' || tail == ':' || tail == '·')
            {
                baseWeight += 1.1f;
            }

            return baseWeight;
        }
    }
}
