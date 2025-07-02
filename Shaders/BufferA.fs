in vec3 aColor;
in vec4 aPosition;
out vec4 C;
uniform sampler2D iChannel0;
uniform sampler2D iChannel1;
uniform sampler2D iChannel2;
uniform sampler2D iChannel3;
uniform vec2 iResolution;
uniform vec4 iMouse;
uniform float iTime;
uniform int iFrame;


#define PI 3.1459
#define TAU PI*2.0

#define CYCLE_DURATION 30.0 
#define SUNRISE_TIME 0.25    
#define SUNSET_TIME 0.75   

const vec3 COLOR_NIGHT = vec3(0.05, 0.05, 0.15);
const vec3 COLOR_SUNRISE = vec3(0.8, 0.4, 0.2);
const vec3 COLOR_DAY = vec3(0.4, 0.7, 1.0);
const vec3 COLOR_SUNSET = vec3(0.9, 0.3, 0.1);

const vec3 CLOUD_COLOR_DAY = vec3(1.0, 1.0, 1.0);
const vec3 CLOUD_COLOR_SUNSET = vec3(1.0, 0.8, 0.6);
const vec3 STAR_COLOR = vec3(0.8, 0.9, 1.0);

const vec3 COLOR_BACKGROUND = vec3(0.1, 0.01, 0.05);

const vec3 CaixaAguaPosition = vec3(5.0, 1.0, -0.2); 
const float CaixaAguaHeight = 6.5;
const float CaixaAguaRadius = 0.4;

float minDist = 0.01;
float maxDist = 100.;
int maxIt = 100;

struct Surface {
    vec3 color;
    float d;
    float smoothness;
    vec3 emission;
};

float getTimeOfDay() {
    return mod(iTime / CYCLE_DURATION, 1.0);
}

vec3 getSunPosition(float timeOfDay) {
    float angle = timeOfDay * TAU - PI/2.0; 
    float height = sin(angle) * 10.0; 
    float distance = cos(angle) * 15.0; 
    return vec3(distance, max(height, -2.0), 0.0);
}

float sdSphere(vec3 p, float r) {
    return length(p) - r;
}
float hash(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float noise(vec2 p) {
    vec2 i = floor(p);
    vec2 f = fract(p);
    vec2 u = f * f * (3.0 - 2.0 * f);
    
    return mix(mix(hash(i + vec2(0.0, 0.0)),
                   hash(i + vec2(1.0, 0.0)), u.x),
               mix(hash(i + vec2(0.0, 1.0)),
                   hash(i + vec2(1.0, 1.0)), u.x), u.y);
}

float cloudNoise(vec2 p) {
    float f = 0.0;
    f += 0.5000 * noise(p); p *= 2.0;
    f += 0.2500 * noise(p); p *= 2.0;
    f += 0.1250 * noise(p); p *= 2.0;
    f += 0.0625 * noise(p);
    return f;
}


float getClouds(vec3 rayDir, float timeOfDay) {

    if (rayDir.y <= 0.1) return 0.0;
    
    vec2 skyUV = vec2(atan(rayDir.z, rayDir.x) / TAU + 0.5,
                      (rayDir.y - 0.1) / 0.9);
    
    skyUV.x += iTime * 0.02;
    skyUV *= 3.0; 
    
    float cloudDensity = cloudNoise(skyUV);
    
    cloudDensity = smoothstep(0.4, 0.8, cloudDensity);
    
    if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
        cloudDensity *= 0.3;
    }
    
    return cloudDensity;
}

float getStars(vec3 rayDir, float timeOfDay) {

    float starVisibility = 0.0;
    if (timeOfDay < SUNRISE_TIME) {
        starVisibility = 1.0 - timeOfDay / SUNRISE_TIME;
    } else if (timeOfDay > SUNSET_TIME) {
        starVisibility = (timeOfDay - SUNSET_TIME) / (1.0 - SUNSET_TIME);
    }
    
    if (starVisibility <= 0.0) return 0.0;
    
    vec2 starUV = vec2(atan(rayDir.z, rayDir.x) / TAU + 0.5,
                       acos(rayDir.y) / PI);
    
    starUV *= iResolution.x * 0.05;
    
    vec2 gridId = floor(starUV);
    vec2 gridUV = fract(starUV);
    
    float starField = hash(gridId);
    
    starField = step(0.98, starField); 
    
    vec2 starPos = vec2(hash(gridId + vec2(1.0, 0.0)), hash(gridId + vec2(0.0, 1.0)));
    float dist = length(gridUV - starPos);
    
    float starShape = 1.0 - smoothstep(0.0, 0.05, dist);
    starField *= starShape;
    
    float twinkle = 0.7 + 0.3 * sin(iTime * 2.0 + hash(gridId) * 100.0);
    starField *= twinkle;
    
    return starField * starVisibility;
}

vec3 getSkyColor(float timeOfDay, vec3 rayDir) {
    vec3 sunPos = getSunPosition(timeOfDay);
    vec3 sunDir = normalize(sunPos);
    
    float sunDot = max(dot(rayDir, sunDir), 0.0);
    
    vec3 skyColor;
    
    if (timeOfDay < SUNRISE_TIME) {
        float t = timeOfDay / SUNRISE_TIME;
        skyColor = mix(COLOR_NIGHT, COLOR_SUNRISE, smoothstep(0.8, 1.0, t));
    } else if (timeOfDay < 0.5) {
        float t = (timeOfDay - SUNRISE_TIME) / (0.5 - SUNRISE_TIME);
        skyColor = mix(COLOR_SUNRISE, COLOR_DAY, t);
    } else if (timeOfDay < SUNSET_TIME) {
        float t = (timeOfDay - 0.5) / (SUNSET_TIME - 0.5);
        skyColor = mix(COLOR_DAY, COLOR_SUNSET, t);
    } else {
        float t = (timeOfDay - SUNSET_TIME) / (1.0 - SUNSET_TIME);
        skyColor = mix(COLOR_SUNSET, COLOR_NIGHT, smoothstep(0.0, 0.8, t));
    }
    
    float sunGlow = pow(sunDot, 8.0) * 0.5;
    if (sunPos.y > 0.0) { 
        skyColor += vec3(1.0, 0.9, 0.7) * sunGlow;
    }

    float starIntensity = getStars(rayDir, timeOfDay);
    skyColor += STAR_COLOR * starIntensity * 0.8;
    
    float cloudDensity = getClouds(rayDir, timeOfDay);
    if (cloudDensity > 0.0) {
        vec3 cloudColor;
        if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
            cloudColor = mix(skyColor, vec3(0.2, 0.2, 0.3), 0.7);
        } else if (timeOfDay < 0.4 || timeOfDay > 0.6) {
            cloudColor = CLOUD_COLOR_SUNSET;
        } else {
            cloudColor = CLOUD_COLOR_DAY;
        }
        
        skyColor = mix(skyColor, cloudColor, cloudDensity * 0.8);
    }
    
    
    return skyColor;
}

float getLightIntensity(float timeOfDay) {
    if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
        return 0.1;
    } else {
        float dayProgress = (timeOfDay - SUNRISE_TIME) / (SUNSET_TIME - SUNRISE_TIME);
        return 0.1 + 0.9 * sin(dayProgress * PI); 
    }
}

mat4 rotX (in float angle)
{
    float rad = radians (angle);
    float c = cos (rad);
    float s = sin (rad);
    mat4 mat = mat4 (vec4 (1.0, 0.0, 0.0, 0.0),
                     vec4 (0.0,   c,   s, 0.0),
                     vec4 (0.0,  -s,   c, 0.0),
                     vec4 (0.0, 0.0, 0.0, 1.0));
    return mat;
}

mat4 rotY (in float angle)
{
    float rad = radians (angle);
    float c = cos (rad);
    float s = sin (rad);
    mat4 mat = mat4 (vec4 (  c, 0.0,  -s, 0.0),
                     vec4 (0.0, 1.0, 0.0, 0.0),
                     vec4 (  s, 0.0,   c, 0.0),
                     vec4 (0.0, 0.0, 0.0, 1.0));
    return mat;
}

mat4 rotZ (in float angle)
{
    float rad = radians (angle);
    float c = cos (rad);
    float s = sin (rad);
    mat4 mat = mat4 (vec4 (  c,   s, 0.0, 0.0),
                     vec4 ( -s,   c, 0.0, 0.0),
                     vec4 (0.0, 0.0, 1.0, 0.0),
                     vec4 (0.0, 0.0, 0.0, 1.0));
    return mat;
}

vec3 opTransf (vec3 p, mat4 m)
{
    return (m * vec4 (p, 1.)).xyz;
}

float sdBox(vec3 p, vec3 b) {
    vec3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}

float sdCylinder( vec3 p, float h, float r )
{
  vec2 d = abs(vec2(length(p.xz),p.y)) - vec2(r,h/2.0);
  return min(max(d.x,d.y),0.0) + length(max(d,0.0));
}

Surface getCaixaAguaMaterial(vec3 p, Surface s) {
    
    vec3 corDaBase = vec3(0.020, 0.325, 0.486); // Cor #05537c
    vec3 corDoTopo = vec3(0.953, 0.522, 0.078); // Cor #f38514

    float alturaDaBaseNoMundo = CaixaAguaPosition.y - (CaixaAguaHeight / 2.0);
    float alturaRelativa = (p.y - alturaDaBaseNoMundo) / CaixaAguaHeight;

    if (alturaRelativa > 0.8) {
        s.color = corDoTopo;
    } else {
        s.color = corDaBase;
    }

    s.smoothness = 0.8;
    
    return s;
}

float sdWavyGround(vec3 p, vec2 center) {
    

    float flatRadius = 30.0; 
    float hillRadius = 60.0;
    float distFromCenter = distance(p.xz, center);
    float blendFactor = smoothstep(flatRadius, hillRadius, distFromCenter);
    float hillHeight = 1.5 * sin(p.x * 0.2) * cos(p.z * 0.2) + 0.5 * sin(p.z * 0.5);
    float finalHeight = mix(0.0, hillHeight, blendFactor);
    
    return p.y - finalHeight;
}

float sphereDist(vec3 p,vec4 cr)
{
   vec3 c = cr.xyz-p;
   float r = cr.w;
   r+= 0.3*pow((0.5+0.5*sin(2.0*PI*iTime*1.2 - c.y*0.5)),4.0);
   c.x=abs(c.x);
   c.z*=1.2;
   c.y+= c.x*sqrt(abs((2.0-c.x)));
    return length(c)-r;
}

float planeDist(vec3 p,vec4 nd)
{
    return dot(p,nd.xyz)-nd.w;
}

Surface unionS(Surface s1,Surface s2)
{
    return (s1.d<s2.d)? s1:s2;
}

float sdTorus( vec3 p, vec2 t )
{
    vec2 q = vec2(length(p.xz)-t.x, p.y);
    return length(q)-t.y;
}

Surface mapCalcada(vec3 p, vec3 position, vec3 size, float angleY) {
    
    mat4 rotation = rotY(angleY);
    vec3 p_centered = p - position;
    vec3 p_rotated = opTransf(p_centered, rotation);
    
    float distCalcada = sdBox(p_rotated, size);

    vec3 corConcreto = vec3(0.6);
    vec3 corLinha = vec3(0.4);
    float tamanhoDoBloco = 0.2;
    float larguraDaLinha = 0.02;
    
    float linhaX = smoothstep(0.0, 0.005, larguraDaLinha - min(abs(mod(p_rotated.x, tamanhoDoBloco) - tamanhoDoBloco/2.0), abs(mod(p_rotated.z, tamanhoDoBloco) - tamanhoDoBloco/2.0)));
    float linhaZ = smoothstep(0.0, 0.005, larguraDaLinha - min(abs(mod(p_rotated.z, tamanhoDoBloco) - tamanhoDoBloco/2.0), abs(mod(p_rotated.x, tamanhoDoBloco) - tamanhoDoBloco/2.0)));
    float linhas = max(linhaX, linhaZ);

    Surface calcada;
    calcada.color = mix(corConcreto, corLinha, linhas); 
    calcada.smoothness = 0.1;
    calcada.emission = vec3(0.0);
    calcada.d = distCalcada;
    
    return calcada;
}

Surface getSceneDist(vec3 p)
{
    // Titanic
    vec3 titanicPosition = vec3(0.0, 1.0, 5.0);
    vec3 titanicSize = vec3(6.0, 3.0, 2.0);
    
    vec3 p_centered_corpo = p - titanicPosition;
    mat4 rotation_titanic = rotY(0.0); 
    vec3 p_rotated_corpo = opTransf(p_centered_corpo, rotation_titanic);
    
    float corpoDist = sdBox(p_rotated_corpo, titanicSize);
    Surface Corpo;
    Corpo.color = vec3(0.8, 0.7, 0.6);
    Corpo.smoothness = 0.2;
    Corpo.emission = vec3(0.0);
    Corpo.d = corpoDist;

    float tetoRaio = titanicSize.z;
    float tetoComprimento = 12;

    vec3 tetoPosition = titanicPosition + vec3(0.0, titanicSize.y / 2.0 , 0.0);

    vec3 p_centered_teto = p - tetoPosition;
    mat4 rotation_teto = rotZ(90.0); 
    vec3 p_rotated_teto = opTransf(p_centered_teto, rotation_teto);
    
    float tetoDist = sdCylinder(p_rotated_teto, tetoComprimento, tetoRaio);
    Surface Teto;
    Teto.color = vec3(0.8, 0.7, 0.6);
    Teto.smoothness = 0.3;
    Teto.emission = vec3(0.0);
    Teto.d = tetoDist;


    // Caixa da água
    Surface CaixaAgua;
    CaixaAgua.color = vec3(0.7, 0.7, 0.7);
    CaixaAgua.smoothness = 0.8;
    CaixaAgua.emission = vec3(0.0);

    vec3 p_local_caixa = p - CaixaAguaPosition;
    float distCilindro = sdCylinder(p_local_caixa, CaixaAguaHeight, CaixaAguaRadius);
    float todasAsDistanciasAneis = maxDist;
    vec2  tamanhoDoAnel = vec2(CaixaAguaRadius, 0.05);

    for (int i = 0; i < 3; i++) {
        float alturaAnel = (CaixaAguaHeight / 2.0 - 0.5) * (float(i) - 1.0);
        float distAnelAtual = sdTorus(p_local_caixa - vec3(0.0, alturaAnel, 0.0), tamanhoDoAnel);
        todasAsDistanciasAneis = min(todasAsDistanciasAneis, distAnelAtual);
    }
    
    if (abs(p_local_caixa.y) < CaixaAguaHeight / 2.0) {
        CaixaAgua.d = min(distCilindro, todasAsDistanciasAneis);
    } else {
        CaixaAgua.d = distCilindro;
    }

    // Auditórios
    Surface Auditorios;
    Auditorios.smoothness = 0.2;
    Auditorios.emission = vec3(0.0);
    Auditorios.color = vec3(0.3, 0.35, 0.4); 
    vec3 AuditoriosPosition = vec3(-13.0, 1.0, 7.0);
    vec3 AuditoriosSize = vec3(2.5, 1, 1.5);
    vec3 p_centered_aud = p - AuditoriosPosition;
    mat4 rotationMatrix_aud = rotY(105.0); 
    vec3 p_rotated_aud = opTransf(p_centered_aud, rotationMatrix_aud);
    Auditorios.d = sdBox(p_rotated_aud, AuditoriosSize);

    // Biblioteca
    Surface Biblioteca;
    Biblioteca.smoothness = 0.2;
    Biblioteca.emission = vec3(0.0);
    Biblioteca.color = vec3(0.3, 0.35, 0.4); 
    vec3 BibliotecaPosition = vec3(-9.0, 1.0, -4.0);
    vec3 BibliotecaSize = vec3(2.5, 0.7, 1.5);
    vec3 p_centered_bib = p - BibliotecaPosition;
    mat4 rotationMatrix_bib = rotY(105.0); 
    vec3 p_rotated_bib = opTransf(p_centered_bib, rotationMatrix_bib);
    Biblioteca.d = sdBox(p_rotated_bib, BibliotecaSize);

    // Queijo
    Surface Queijo;
    Queijo.smoothness = 0.2;
    Queijo.color = vec3(0.8, 0.7, 0.6);
    vec3 QueijoPosition = vec3(-11.7, 0.2, 2.0); 
    float QueijoHeight = 0.3;            
    float QueijoRadius = 0.6;            
    Queijo.d = sdCylinder(p - QueijoPosition, QueijoHeight, QueijoRadius);

    // Elefante Branco
    Surface Elefante;
    Elefante.smoothness = 0.2;
    Elefante.emission = vec3(0.0);
    Elefante.color = vec3(0.3, 0.35, 0.4); 
    vec3 ElefantePosition = vec3(14.0, 1.0, 3.0);
    vec3 ElefanteSize = vec3(4.0, 2.0, 1.5);
    vec3 p_centered_ele = p - ElefantePosition;
    mat4 rotationMatrix_ele = rotY(0.0); 
    vec3 p_rotated_ele = opTransf(p_centered_ele, rotationMatrix_ele);
    Elefante.d = sdBox(p_rotated_ele, ElefanteSize);

    // Chão
    Surface Ground;
    Ground.color = vec3(0.3, 0.6, 0.2);
    Ground.smoothness = 0.1;
    Ground.emission = vec3(0.0);
    Ground.d = sdWavyGround(p, titanicPosition.xz);


    // Janelas
    Surface materialJanelaAcesa;
    materialJanelaAcesa.color = vec3(0.05); 
    materialJanelaAcesa.emission = vec3(1.0, 0.8, 0.5) * 1.5;
    materialJanelaAcesa.smoothness = 0.8; 

    Surface materialJanelaApagada;
    materialJanelaApagada.color = vec3(0.1, 0.1, 0.15);
    materialJanelaApagada.emission = vec3(0.0);
    materialJanelaApagada.smoothness = 0.9;

    Surface Titanic = unionS(Corpo, Teto);
    Titanic = unionS(Titanic, CaixaAgua);
    Titanic = unionS(Titanic, Auditorios);
    Titanic = unionS(Titanic, Biblioteca);
    Titanic = unionS(Titanic, Queijo);
    Titanic = unionS(Titanic, Elefante);

    vec3  tamanhoDaJanela = vec3(0.3, 0.3, 0.02);
    int   NUM_COLUNAS = 16;
    int   NUM_FILEIRAS = 5;
    float ESPACO_HORIZONTAL = 0.7;
    float ESPACO_VERTICAL = 0.7;
    vec3  inicioDaGrade = vec3(-5.2, 3.5, 3.0);

    for (int j = 0; j < NUM_FILEIRAS; j++) {
        for (int i = 0; i < NUM_COLUNAS; i++) {
            
            Surface janelaAtual;
            
            if (iTime >= 20.0 && iTime <= 21.5) {
  
                if (
                    // F
                    (i==3 && j==0) ||
                     (i==3 && j==1) ||
                     (i==3 && j==2) ||
                     (i==3 && j==3) ||
                     (i==3 && j==4)
                     || (i==4 && j==0)
                     || (i==5 && j==0)
                     || (i==6 && j==0)
                     || (i==4 && j==2)
                     || (i==5 && j==2)
                     || (i==6 && j==2)

                     // E
                     || (i==8 && j==0)
                     || (i==8 && j==1)
                     || (i==8 && j==2)
                     || (i==8 && j==3)
                     || (i==8 && j==4)
                     || (i==9 && j==0)
                     || (i==10 && j==0)
                     || (i==11 && j==0)
                     || (i==9 && j==2)
                     || (i==10 && j==2)
                     || (i==11 && j==2)
                     || (i==9 && j==4)
                     || (i==10 && j==4)
                     || (i==11 && j==4)

                )
                {
                    janelaAtual = materialJanelaAcesa;
                } else {
                    janelaAtual = materialJanelaApagada;
                }
            }

            else if (iTime > 21.5 && iTime <= 23.0) { 
                
                if (

                    // L
                     (i==2 && j==0)
                     || (i==2 && j==1)
                     || (i==2 && j==2)
                     || (i==2 && j==3)
                     || (i==2 && j==4)
                     || (i==3 && j==4)
                     || (i==4 && j==4)
                     || (i==5 && j==4)

                    // I
                    || (i==7 && j==0)
                    || (i==7 && j==1)
                    || (i==7 && j==2)
                    || (i==7 && j==3)
                    || (i==7 && j==4)

                    // Z
                    || (i==9 && j==0)
                    || (i==10 && j==0)
                    || (i==11 && j==0)
                    || (i==12 && j==0)
                    || (i==13 && j==0)

                    || (i==12 && j==1)
                    || (i==11 && j==2)
                    || (i==10 && j==3)

                    || (i==9 && j==4)
                    || (i==10 && j==4)
                    || (i==11 && j==4)
                    || (i==12 && j==4)
                    || (i==13 && j==4)
                ) 
                {
                    janelaAtual = materialJanelaAcesa;
                } else {
                    janelaAtual = materialJanelaApagada;
                }

            }

            else if (iTime > 23.0 && iTime <= 24.5) { 
                
                if (

                    // 2
                    (i==2 && j==0)
                    || (i==3 && j==0)
                    || (i==4 && j==0)
                    || (i==5 && j==0)
                    || (i==6 && j==0)

                    || (i==6 && j==1)

                    || (i==2 && j==2)
                    || (i==3 && j==2)
                    || (i==4 && j==2)
                    || (i==5 && j==2)
                    || (i==6 && j==2)

                    || (i==2 && j==3)

                    || (i==2 && j==4)
                    || (i==3 && j==4)
                    || (i==4 && j==4)
                    || (i==5 && j==4)
                    || (i==6 && j==4)

                    // 0
                    || (i==9 && j==0)
                    || (i==10 && j==0)
                    || (i==11 && j==0)
                    || (i==12 && j==0)

                    || (i==9 && j==1)
                    || (i==9 && j==2)
                    || (i==9 && j==3)
                    || (i==9 && j==4)

                    || (i==10 && j==4)
                    || (i==11 && j==4)
                    || (i==12 && j==4)

                    || (i==12 && j==1)
                    || (i==12 && j==2)
                    || (i==12 && j==3)
                ) 
                {
                    janelaAtual = materialJanelaAcesa;
                } else {
                    janelaAtual = materialJanelaApagada;
                }

            }

            else if (iTime >  24.5 && iTime <= 26.0) { 

                if (

                    // A
                    (i==4 && j==0)
                    || (i==3 && j==1)
                    || (i==2 && j==2)
                    || (i==1 && j==3)
                    || (i==0 && j==4)

                    || (i==5 && j==1)
                    || (i==6 && j==2)
                    || (i==7 && j==3)
                    || (i==8 && j==4)

                    || (i==2 && j==3)
                    || (i==3 && j==3)
                    || (i==4 && j==3)
                    || (i==5 && j==3)
                    || (i==6 && j==3)

                    // N 
                    || (i==10 && j==0)
                    || (i==10 && j==1)
                    || (i==10 && j==2)
                    || (i==10 && j==3)
                    || (i==10 && j==4)

                    || (i==15 && j==0)
                    || (i==15 && j==1)
                    || (i==15 && j==2)
                    || (i==15 && j==3)
                    || (i==15 && j==4)

                    || (i==11 && j==1)
                    || (i==12 && j==2)
                    || (i==13 && j==3)
                    || (i==14 && j==4)


                ) 
                {
                    janelaAtual = materialJanelaAcesa;
                } else {
                    janelaAtual = materialJanelaApagada;
                }

            }

            else if (iTime >  26.0 && iTime <= 29.0) { 

                if (

                    // O
                    (i==2 && j==0)
                    || (i==3 && j==0)
                    || (i==4 && j==0)
                    || (i==5 && j==0)

                    || (i==2 && j==0)
                    || (i==2 && j==1)
                    || (i==2 && j==2)
                    || (i==2 && j==3)
                    || (i==2 && j==4)

                    || (i==3 && j==4)
                    || (i==4 && j==4)
                    || (i==5 && j==4)

                    || (i==5 && j==1)
                    || (i==5 && j==2)
                    || (i==5 && j==3)

                    // S
                    || (i==7 && j==0)
                    || (i==8 && j==0)
                    || (i==9 && j==0)
                    || (i==10 && j==0)
                    || (i==11 && j==0)

                    || (i==7 && j==1)

                    || (i==7 && j==2)
                    || (i==8 && j==2)
                    || (i==9 && j==2)
                    || (i==10 && j==2)
                    || (i==11 && j==2)

                    || (i==11 && j==3)

                    || (i==7 && j==4)
                    || (i==8 && j==4)
                    || (i==9 && j==4)
                    || (i==10 && j==4)
                    || (i==11 && j==4)

                    // !
                    || (i==13 && j==0)
                    || (i==13 && j==1)
                    || (i==13 && j==2)

                    || (i==13 && j==4)

                ) 
                {
                    janelaAtual = materialJanelaAcesa;
                } else {
                    janelaAtual = materialJanelaApagada;
                }

            }

            else if (iTime >  29.0) { 

                janelaAtual = materialJanelaAcesa;

            }

            else {
                janelaAtual = materialJanelaApagada;
            }

            vec3 posJanelaAtual = inicioDaGrade + vec3(float(i) * ESPACO_HORIZONTAL, -float(j) * ESPACO_VERTICAL, 0.0);
            janelaAtual.d = sdBox(p - posJanelaAtual, tamanhoDaJanela);
            
            Titanic = unionS(Titanic, janelaAtual);
        }
    }

    Surface scene = unionS(Ground, Titanic);

    // Calçadas
    Surface calcada1 = mapCalcada(
        p,                               
        vec3(-6.0, 0.05, -2.0),      
        vec3(1.0, 0.1, 4.5),  
        15.0             
    );

    Surface calcada2 = mapCalcada(
        p,                     
        vec3(-11.0, 0.05, 2.0),       
        vec3(5.0, 0.1, 3.0),           
        0.0            
    );
    
  
    Surface calcada3 = mapCalcada(
        p,                          
        vec3(-11.0, 0.05, 2.0),          
        vec3(10.0, 0.1, 0.6),        
        0.0                     
    );

    Surface calcada4 = mapCalcada(
        p,                       
        vec3(-11.0, 0.05, -0.5),     
        vec3(30.0, 0.1, 0.9),            
        0.0                    
    );

    Surface calcada5 = mapCalcada(
        p,                        
        vec3(8.0, 0.05, 3.0),         
        vec3(0.8, 0.1, 4),            
        0.0                       
    );

    Surface calcada6 = mapCalcada(
        p,                            
        vec3(5.0, 0.05, 3.0),     
        vec3(3, 0.1, 1),             
        0.0                   
    );

    scene = unionS(scene, calcada1);
    scene = unionS(scene, calcada2);
    scene = unionS(scene, calcada3);
    scene = unionS(scene, calcada4);
    scene = unionS(scene, calcada5);
    scene = unionS(scene, calcada6);

    // Pessoas
    vec3 caminho0_ida = vec3(-10.0, 0.25, -1.1);
    vec3 caminho0_volta   = vec3(10.0, 0.25, -1.1);

    vec3 caminho1_ida = vec3(-13.0, 0.25, 3.5);
    vec3 caminho1_volta   = vec3(5.0, 0.25, 3.5);

    vec3 caminho2_ida = vec3(8.0, 0.25, 0.0);
    vec3 caminho2_volta   = vec3(8.0, 0.25, 8.0);

    float velocidade0 = 5.0;
    vec3 direcaoDoCaminho0 = normalize(caminho1_volta - caminho1_ida);
    float progressoNoCaminho0 = mod(iTime * velocidade0, distance(caminho1_ida, caminho1_volta));
    vec3 posicaoBase0 = caminho1_ida + direcaoDoCaminho0 * progressoNoCaminho0;
    posicaoBase0.y += 0.03 * abs(sin(iTime * 15.0));
    Surface Tronco0 = Surface(vec3(0.8, 0.2, 0.2), sdBox(p - posicaoBase0, vec3(0.15, 0.4, 0.1)), 0.1, vec3(0.0));
    Surface Cabeca0 = Surface(vec3(0.9, 0.7, 0.5), sdSphere(p - (posicaoBase0 + vec3(0.0, 0.4/2.0 + 0.15, 0.0)), 0.15), 0.1, vec3(0.0));
    Surface Pessoa0 = unionS(Tronco0, Cabeca0);

    float velocidade2 = 5.2;
    vec3 direcaoDoCaminho2 = normalize(caminho1_volta - caminho1_ida);
    float progressoNoCaminho2 = mod(iTime * velocidade2 + 2.0, distance(caminho1_ida, caminho1_volta)); // Offset
    vec3 posicaoBase2 = caminho1_ida + direcaoDoCaminho2 * progressoNoCaminho2;
    posicaoBase2.y += 0.03 * abs(sin(iTime * 16.0));
    Surface Tronco2 = Surface(vec3(0.2, 0.8, 0.3), sdBox(p - posicaoBase2, vec3(0.15, 0.4, 0.1)), 0.1, vec3(0.0));
    Surface Cabeca2 = Surface(vec3(0.8, 0.6, 0.4), sdSphere(p - (posicaoBase2 + vec3(0.0, 0.4/2.0 + 0.15, 0.0)), 0.15), 0.1, vec3(0.0));
    Surface Pessoa2 = unionS(Tronco2, Cabeca2);

    float velocidade3 = 5.0;
    vec3 direcaoDoCaminho3 = normalize(caminho2_volta - caminho2_ida);
    float progressoNoCaminho3 = mod(iTime * velocidade3, distance(caminho2_ida, caminho2_volta));
    vec3 posicaoBase3 = caminho2_ida + direcaoDoCaminho3 * progressoNoCaminho3;
    posicaoBase3.y += 0.03 * abs(sin(iTime * 15.5));
    Surface Tronco3 = Surface(vec3(0.5, 0.1, 0.5), sdBox(p - posicaoBase3, vec3(0.15, 0.4, 0.1)), 0.1, vec3(0.0));
    Surface Cabeca3 = Surface(vec3(0.4, 0.2, 0.1), sdSphere(p - (posicaoBase3 + vec3(0.0, 0.4/2.0 + 0.15, 0.0)), 0.15), 0.1, vec3(0.0));
    Surface Pessoa3 = unionS(Tronco3, Cabeca3);

    float velocidade4 = 5.1;
    vec3 direcaoDoCaminho4 = normalize(caminho2_ida - caminho2_volta);
    float progressoNoCaminho4 = mod(iTime * velocidade4, distance(caminho2_ida, caminho2_volta));
    vec3 posicaoBase4 = caminho2_volta + direcaoDoCaminho4 * progressoNoCaminho4;
    posicaoBase4.y += 0.03 * abs(sin(iTime * 14.8));
    Surface Tronco4 = Surface(vec3(0.9, 0.9, 0.1), sdBox(p - posicaoBase4, vec3(0.15, 0.4, 0.1)), 0.1, vec3(0.0));
    Surface Cabeca4 = Surface(vec3(0.9, 0.7, 0.5), sdSphere(p - (posicaoBase4 + vec3(0.0, 0.4/2.0 + 0.15, 0.0)), 0.15), 0.1, vec3(0.0));
    Surface Pessoa4 = unionS(Tronco4, Cabeca4);

    float velocidade5 = 5.0;
    vec3 direcaoDoCaminho5 = normalize(caminho0_volta - caminho0_ida);
    float progressoNoCaminho5 = mod(iTime * velocidade5, distance(caminho0_ida, caminho0_volta));
    vec3 posicaoBase5 = caminho0_ida + direcaoDoCaminho5 * progressoNoCaminho5;
    posicaoBase5.y += 0.03 * abs(sin(iTime * 15.0));
    Surface Tronco5 = Surface(vec3(0.8, 0.2, 0.2), sdBox(p - posicaoBase5, vec3(0.15, 0.4, 0.1)), 0.1, vec3(0.0));
    Surface Cabeca5 = Surface(vec3(0.9, 0.7, 0.5), sdSphere(p - (posicaoBase5 + vec3(0.0, 0.4/2.0 + 0.15, 0.0)), 0.15), 0.1, vec3(0.0));
    Surface Pessoa5 = unionS(Tronco5, Cabeca5);

    float velocidade6 = 5.6;
    vec3 direcaoDoCaminho6 = normalize(caminho0_ida - caminho0_volta);
    float progressoNoCaminho6 = mod(iTime * velocidade6, distance(caminho0_volta, caminho0_ida));
    vec3 posicaoBase6 = caminho0_volta + direcaoDoCaminho6 * progressoNoCaminho6;
    posicaoBase6.y += 0.03 * abs(sin(iTime * 15.0));
    Surface Tronco6 = Surface(vec3(0.8, 0.2, 0.2), sdBox(p - posicaoBase6, vec3(0.15, 0.4, 0.1)), 0.1, vec3(0.0));
    Surface Cabeca6 = Surface(vec3(0.4, 0.2, 0.1), sdSphere(p - (posicaoBase6 + vec3(0.0, 0.4/2.0 + 0.15, 0.0)), 0.15), 0.1, vec3(0.0));
    Surface Pessoa6 = unionS(Tronco6, Cabeca6);

    scene = unionS(scene, Pessoa0);
    scene = unionS(scene, Pessoa2);
    scene = unionS(scene, Pessoa3);
    scene = unionS(scene, Pessoa4);
    scene = unionS(scene, Pessoa5);
    scene = unionS(scene, Pessoa6);

    return scene;
}

Surface rayMarching(vec3 ro,vec3 rd)
{
    int i=0;
    float da=0.0;
    vec3 p = ro+da*rd;
    Surface d_o =getSceneDist(p);
    while ((da<maxDist)&&(d_o.d>minDist)&&(i<maxIt))
    {
        da+=d_o.d;
        p =ro+da*rd;
        d_o =getSceneDist(p);
        i++;
    }
    if((i<maxIt)&&(da<maxDist))
        d_o.d =da;
    else
        d_o.d = maxDist;
    return d_o;
}

vec3 estimateNormal(vec3 p)
{
    float ep = 0.01;
    float d = getSceneDist(p).d;
    vec3 n =vec3(d-getSceneDist(vec3(p.x-ep,p.y,p.z)).d,
                  d-getSceneDist(vec3(p.x,p.y-ep,p.z)).d,
                  d-getSceneDist(vec3(p.x,p.y,p.z-ep)).d);
    return normalize(n);
}

vec3 getLight(vec3 p,Surface s,vec3 CamPos)
{
    float timeOfDay = getTimeOfDay();
    vec3 sunPos = getSunPosition(timeOfDay);
    float lightIntensity = getLightIntensity(timeOfDay);
    
    vec3 lColor;
    if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
        lColor = vec3(0.3, 0.3, 0.6);
    } else if (timeOfDay < 0.4 || timeOfDay > 0.6) {
        lColor = vec3(1.0, 0.7, 0.4); 
    } else {
        lColor = vec3(1.0, 1.0, 0.9); 
    }
    
    vec3 ld = normalize(sunPos - p);
    vec3 n = estimateNormal(p);
    float r = clamp(dot(ld,n), 0.0, 1.0);
    
    float ka = 0.2 + 0.1 * lightIntensity; 
    float kd = 0.5 * lightIntensity;
    float ks = 0.20 * lightIntensity;
    
    vec3 eye = normalize(p - CamPos);
    vec3 R = normalize(reflect(n, ld));
    float phi = clamp(dot(R, eye), 0.0, 1.0);
    
    vec3 col = s.color * ka + s.color * r * kd + lColor * ks * pow(phi, 10.0);
    
    Surface ss = rayMarching(p + 100.0 * minDist * n, ld);
    if(ss.d < length(p - sunPos))
        col *= 0.2;

    return col;
}

mat3 setCamera(vec3 CamPos,vec3 Look_at)
{
    vec3 cd =normalize(Look_at-CamPos);
    vec3 cv = normalize(cross(vec3(0.0,1.0,0.0),cd));
    vec3 cu = cross(cd,cv);
    return mat3(cv,cu,cd);
}

void main ()
{
    vec2 uv = (gl_FragCoord.xy-0.5*iResolution.xy)/iResolution.y;
    uv.x *= iResolution.x/iResolution.y;

    vec2 m = iMouse.xy / iResolution.xy;
    vec3 Target = vec3(0.0,1.0,6.0);
    vec3 Cam = Target -  vec3(20.0*cos(PI*0.5 +m.x*iMouse.z),-5.0,16.0*sin(PI*0.5+m.y*iMouse.z));
    mat3 M =setCamera(Cam,Target);
    vec3 rd = M * normalize(vec3(uv,1.0));

    Surface sd = rayMarching(Cam,rd);
    vec3 col;

    if (sd.d < maxDist)
    {
        vec3 p = Cam + sd.d * rd;
        Surface finalSurface = sd; 

        if (finalSurface.color == vec3(0.7, 0.7, 0.7)) {
            finalSurface = getCaixaAguaMaterial(p, finalSurface);
        }

        col = getLight(p, finalSurface, Cam) + finalSurface.emission;
    }
    else 
    {
        float timeOfDay = getTimeOfDay();
        col = getSkyColor(timeOfDay, rd);
    }
    
    // Aplica a correção de gamma no final
    col = pow( col, vec3(0.4545) );
    C = vec4(col,1.0);
}