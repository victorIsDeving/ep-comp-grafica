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

// Day/Night cycle parameters
#define CYCLE_DURATION 30.0  // 30 seconds for full day cycle
#define SUNRISE_TIME 0.25    // 25% through cycle
#define SUNSET_TIME 0.75     // 75% through cycle

// Sky colors for different times of day
const vec3 COLOR_NIGHT = vec3(0.05, 0.05, 0.15);
const vec3 COLOR_SUNRISE = vec3(0.8, 0.4, 0.2);
const vec3 COLOR_DAY = vec3(0.4, 0.7, 1.0);
const vec3 COLOR_SUNSET = vec3(0.9, 0.3, 0.1);

// Cloud and star parameters
const vec3 CLOUD_COLOR_DAY = vec3(1.0, 1.0, 1.0);
const vec3 CLOUD_COLOR_SUNSET = vec3(1.0, 0.8, 0.6);
const vec3 STAR_COLOR = vec3(0.8, 0.9, 1.0);

const vec3 COLOR_BACKGROUND = vec3(0.1, 0.01, 0.05);

/*  Install  Istructions

sudo apt-get install g++ cmake git
 sudo apt-get install libsoil-dev libglm-dev libassimp-dev libglew-dev libglfw3-dev libxinerama-dev libxcursor-dev
libxi-dev libfreetype-dev libgl1-mesa-dev xorg-dev

git clone https://github.com/JoeyDeVries/LearnOpenGL.git*/

float minDist = 0.01;
float maxDist = 100.;
int maxIt = 100;

struct Surface {
    vec3 color;
    float d;
};

// Function to get current time of day (0.0 = midnight, 0.5 = noon, 1.0 = midnight)
float getTimeOfDay() {
    return mod(iTime / CYCLE_DURATION, 1.0);
}

// Function to calculate sun position based on time of day
vec3 getSunPosition(float timeOfDay) {
    float angle = timeOfDay * TAU - PI/2.0; // Start at sunrise
    float height = sin(angle) * 10.0; // Sun height
    float distance = cos(angle) * 15.0; // Sun distance
    return vec3(distance, max(height, -2.0), 0.0);
}

// Simple noise function for procedural generation
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

// Function to generate clouds
float cloudNoise(vec2 p) {
    float f = 0.0;
    f += 0.5000 * noise(p); p *= 2.0;
    f += 0.2500 * noise(p); p *= 2.0;
    f += 0.1250 * noise(p); p *= 2.0;
    f += 0.0625 * noise(p);
    return f;
}

// Function to get cloud density at a given sky position
float getClouds(vec3 rayDir, float timeOfDay) {
    // Only render clouds if ray is pointing upward
    if (rayDir.y <= 0.1) return 0.0;
    
    // Project ray onto sky sphere
    vec2 skyUV = vec2(atan(rayDir.z, rayDir.x) / TAU + 0.5,
                      (rayDir.y - 0.1) / 0.9);
    
    // Animate clouds moving across the sky
    skyUV.x += iTime * 0.02; // Cloud movement speed
    skyUV *= 3.0; // Cloud scale
    
    float cloudDensity = cloudNoise(skyUV);
    
    // Make clouds more defined
    cloudDensity = smoothstep(0.4, 0.8, cloudDensity);
    
    // Reduce cloud density at night
    if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
        cloudDensity *= 0.3;
    }
    
    return cloudDensity;
}

// Function to generate stars
float getStars(vec3 rayDir, float timeOfDay) {
    // Only show stars at night
    float starVisibility = 0.0;
    if (timeOfDay < SUNRISE_TIME) {
        starVisibility = 1.0 - timeOfDay / SUNRISE_TIME;
    } else if (timeOfDay > SUNSET_TIME) {
        starVisibility = (timeOfDay - SUNSET_TIME) / (1.0 - SUNSET_TIME);
    }
    
    if (starVisibility <= 0.0) return 0.0;
    
    // Project ray onto sky sphere for star positioning
    vec2 starUV = vec2(atan(rayDir.z, rayDir.x) / TAU + 0.5,
                       acos(rayDir.y) / PI);
    
    // Scale based on screen resolution for consistent star size
    starUV *= iResolution.x * 0.05; // Adjust multiplier for star density
    
    // Get the grid cell
    vec2 gridId = floor(starUV);
    vec2 gridUV = fract(starUV);
    
    // Create star field using noise
    float starField = hash(gridId);
    
    // Make stars more sparse and defined
    starField = step(0.98, starField); // Higher threshold for fewer stars
    
    // Create sharp star points by checking distance to center of cell
    vec2 starPos = vec2(hash(gridId + vec2(1.0, 0.0)), hash(gridId + vec2(0.0, 1.0)));
    float dist = length(gridUV - starPos);
    
    // Make stars small and sharp
    float starShape = 1.0 - smoothstep(0.0, 0.05, dist);
    starField *= starShape;
    
    // Add twinkling effect
    float twinkle = 0.7 + 0.3 * sin(iTime * 2.0 + hash(gridId) * 100.0);
    starField *= twinkle;
    
    return starField * starVisibility;
}

// Function to get sky color based on time of day
vec3 getSkyColor(float timeOfDay, vec3 rayDir) {
    vec3 sunPos = getSunPosition(timeOfDay);
    vec3 sunDir = normalize(sunPos);
    
    // Calculate sun dot product with ray direction
    float sunDot = max(dot(rayDir, sunDir), 0.0);
    
    vec3 skyColor;
    
    if (timeOfDay < SUNRISE_TIME) {
        // Night to sunrise
        float t = timeOfDay / SUNRISE_TIME;
        skyColor = mix(COLOR_NIGHT, COLOR_SUNRISE, smoothstep(0.8, 1.0, t));
    } else if (timeOfDay < 0.5) {
        // Sunrise to noon
        float t = (timeOfDay - SUNRISE_TIME) / (0.5 - SUNRISE_TIME);
        skyColor = mix(COLOR_SUNRISE, COLOR_DAY, t);
    } else if (timeOfDay < SUNSET_TIME) {
        // Noon to sunset
        float t = (timeOfDay - 0.5) / (SUNSET_TIME - 0.5);
        skyColor = mix(COLOR_DAY, COLOR_SUNSET, t);
    } else {
        // Sunset to night
        float t = (timeOfDay - SUNSET_TIME) / (1.0 - SUNSET_TIME);
        skyColor = mix(COLOR_SUNSET, COLOR_NIGHT, smoothstep(0.0, 0.8, t));
    }
    
    // Add sun glow effect
    float sunGlow = pow(sunDot, 8.0) * 0.5;
    if (sunPos.y > 0.0) { // Only during day
        skyColor += vec3(1.0, 0.9, 0.7) * sunGlow;
    }
    
    // Add stars
    float starIntensity = getStars(rayDir, timeOfDay);
    skyColor += STAR_COLOR * starIntensity * 0.8;
    
    // Add clouds
    float cloudDensity = getClouds(rayDir, timeOfDay);
    if (cloudDensity > 0.0) {
        vec3 cloudColor;
        if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
            // Night clouds - darker
            cloudColor = mix(skyColor, vec3(0.2, 0.2, 0.3), 0.7);
        } else if (timeOfDay < 0.4 || timeOfDay > 0.6) {
            // Sunrise/sunset clouds - warm colors
            cloudColor = CLOUD_COLOR_SUNSET;
        } else {
            // Day clouds - white
            cloudColor = CLOUD_COLOR_DAY;
        }
        
        skyColor = mix(skyColor, cloudColor, cloudDensity * 0.8);
    }
    
    
    return skyColor;
}

// Function to get light intensity based on time of day
float getLightIntensity(float timeOfDay) {
    if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
        return 0.1; // Night lighting
    } else {
        float dayProgress = (timeOfDay - SUNRISE_TIME) / (SUNSET_TIME - SUNRISE_TIME);
        return 0.1 + 0.9 * sin(dayProgress * PI); // Smooth day lighting curve
    }
}

// Rotacionar em X
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

// Rotacionar em Y
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

// Rotacionar em Z
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
    return vec4 (m * vec4 (p, 1.)).xyz;
}

//caixa para os prédios
float sdBox(vec3 p, vec3 b) {
    vec3 q = abs(p) - b;
    return length(max(q, 0.0)) + min(max(q.x, max(q.y, q.z)), 0.0);
}
//chão com grama
float sdWavyGround(vec3 p) {
    float height = 0.1 * sin(p.x * 0.5) * sin(p.z * 0.5);
    return p.y - height;
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

Surface getSceneDist(vec3 p)
{
    // Titanic
    vec3 titanicPosition = vec3(0.0, 1.5, 5.0);
    vec3 titanicSize = vec3(0.7, 1.5, 1.3);
    vec3 centered = p - titanicPosition - vec3(0.0, titanicSize.y, 0.0);
    mat4 rot = rotY(radians(90.0));
    vec3 q = opTransf(centered, rot);
    float titanic = sdBox(q + vec3(0.0, titanicSize.y, 0.0), titanicSize);
    Surface Titanic;
    Titanic.color = vec3(0.8, 0.7, 0.6); // Beige color
    Titanic.d = titanic;

    // chão
    Surface Ground;
    Ground.color = vec3(0.3, 0.6, 0.2);
    Ground.d = sdWavyGround(p);

    return unionS(Ground, Titanic);
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
        d_o.d =  maxDist;
    return d_o;
}

vec3 estimateNormal(vec3 p)
{
    float ep = 0.01;
    float d = getSceneDist(p).d;
    vec3 n =vec3 (d-getSceneDist(vec3(p.x-ep,p.y,p.z)).d,d-getSceneDist(vec3(p.x,p.y-ep,p.z)).d,d-getSceneDist(vec3(p.x,p.y,p.z-ep)).d);
    return normalize(n);
}


vec3 getLight(vec3 p,Surface s,vec3 CamPos)
{
    float timeOfDay = getTimeOfDay();
    vec3 sunPos = getSunPosition(timeOfDay);
    float lightIntensity = getLightIntensity(timeOfDay);
    
    // Dynamic light color based on time of day
    vec3 lColor;
    if (timeOfDay < SUNRISE_TIME || timeOfDay > SUNSET_TIME) {
        lColor = vec3(0.3, 0.3, 0.6); // Cool moonlight
    } else if (timeOfDay < 0.4 || timeOfDay > 0.6) {
        lColor = vec3(1.0, 0.7, 0.4); // Warm sunrise/sunset light
    } else {
        lColor = vec3(1.0, 1.0, 0.9); // Neutral daylight
    }
    
    vec3 ld = normalize(sunPos - p);
    vec3 n = estimateNormal(p);
    float r = clamp(dot(ld,n), 0.0, 1.0);
    
    float ka = 0.2 + 0.1 * lightIntensity; // Ambient light varies with time
    float kd = 0.5 * lightIntensity;
    float ks = 0.20 * lightIntensity;
    
    vec3 eye = normalize(p - CamPos);
    vec3 R = normalize(reflect(n, ld));
    float phi = clamp(dot(R, eye), 0.0, 1.0);
    
    vec3 col = s.color * ka + s.color * r * kd + lColor * ks * pow(phi, 10.0);
    
    // Shadow calculation
    Surface ss = rayMarching(p + 100.0 * minDist * n, ld);
    if(ss.d < length(p - sunPos))
        col *= 0.2;

    return col;
}
mat2 Rot(float a) //2D
{
    float s = sin(a);
    float c = cos(a);
    return mat2(c, -s, s, c);
}

mat3 setCamera(vec3 CamPos,vec3 Look_at)
{
    vec3 cd =normalize(Look_at-CamPos);
    vec3 cv = cross(cd,vec3(0.0,1.0,0.0));
    vec3 cu = cross(cv,cd);
    return mat3(cv,cu,cd);
}


void main ()
{
    vec2 uv = (gl_FragCoord.xy-0.5*iResolution.xy)/iResolution.xy;
    float ra =iResolution.x/iResolution.y;
    uv.x*=ra;
    vec2 m = iMouse.xy / iResolution.xy;
    vec3 ro = vec3(0.0,1.0,0.0);
    vec3 Target = vec3(0.0,1.0,6.0);
    vec3 Cam = Target -  vec3(5.0*cos(PI*0.5 +m.x*iMouse.z),-2.0,6.0*sin(PI*0.5+m.y*iMouse.z));
    mat3 M =setCamera(Cam,Target);
    vec3 rd =normalize(vec3(uv.x,uv.y,0.5));
    rd=M*rd;
     Surface sd = rayMarching(Cam,rd);
    float timeOfDay = getTimeOfDay();
    vec3 col = getSkyColor(timeOfDay, rd);
     if (sd.d<maxDist)
     {
        vec3 p = Cam+sd.d*rd;
        col  = getLight(p,sd,Cam);

     }
   col = pow( col, vec3(0.4545) );
C = vec4(col,1.0);

}

