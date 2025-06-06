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
    vec3 lp = vec3 (1.0,7.0,2.0);
    vec3 lColor= vec3(1.0);
    lp.xz+=vec2(sin(iTime),cos(iTime))*3.0;
    vec3 ld = normalize(lp-p);
    vec3 n = estimateNormal(p);
    float r =clamp(dot(ld,n),0.0,1.0);
    float ka =0.3;
    float kd=0.5;
    float ks =0.20;
    vec3 eye = normalize(p-CamPos);
    vec3 R =normalize(reflect(n,ld));
    float phi = clamp(dot(R,eye),0.0,1.0);
    vec3 col=s.color*ka+s.color*r*kd+lColor*ks*pow(phi,10.0);;
    Surface ss =rayMarching(p+100.0*minDist*n,ld);
    if(ss.d<length(p-lp))
        col*=0.2;

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
     vec3 col = COLOR_BACKGROUND;
     if (sd.d<maxDist)
     {
        vec3 p = Cam+sd.d*rd;
        col  = getLight(p,sd,Cam);

     }
   col = pow( col, vec3(0.4545) );
C = vec4(col,1.0);

}

