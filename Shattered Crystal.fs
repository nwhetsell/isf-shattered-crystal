/*{
    "CATEGORIES": [
        "Generator"
    ],
    "CREDIT": "Hyeve <https://www.shadertoy.com/user/Hyeve>",
    "DESCRIPTION": "Converted from <https://www.shadertoy.com/view/ssXcR2>",
    "INPUTS": [
        {
            "NAME": "objectColor",
            "LABEL": "Object Color",
            "TYPE": "color",
            "DEFAULT": [0.5, 0.5, 0.8, 1]
        },
        {
            "NAME": "backgroundColor",
            "LABEL": "Background Color",
            "TYPE": "color",
            "DEFAULT": [0.3, 0.45, 0.7, 1]
        },
        {
            "NAME": "baseSubsurfaceColor",
            "LABEL": "Base Subsurface Color",
            "TYPE": "color",
            "DEFAULT": [0.5, 0.4, 0.6, 1]
        },
        {
            "NAME": "objectSize",
            "LABEL": "Box Size",
            "TYPE": "float",
            "DEFAULT": 2,
            "MAX": 100,
            "MIN": 0
        },
        {
            "NAME": "subsurfaceSize",
            "LABEL": "Subsurface Size",
            "TYPE": "float",
            "DEFAULT": 1.2,
            "MAX": 100,
            "MIN": 0
        }
    ],
    "ISFVSN": "2"
}*/

// Functions from LYGIA <https://github.com/patriciogonzalezvivo/lygia>

// https://github.com/patriciogonzalezvivo/lygia/blob/main/math/rotate2d.glsl
mat2 rotate2d(const in float r) {
    float c = cos(r);
    float s = sin(r);
    return mat2(c, s, -s, c);
}

// https://github.com/patriciogonzalezvivo/lygia/blob/main/sdf/boxSDF.glsl
float boxSDF( vec3 p, vec3 b ) {
    vec3 d = abs(p) - b;
    return min(max(d.x,max(d.y,d.z)),0.0) + length(max(d,0.0));
}


// "shatter" function - subtracts a bunch of semi-random planes from the object
float sh(vec3 p, float d, float a, float s, float o)
{
    // loop for each plane
	for (float i = 0.; i < 9.; i++) {
	    // apply semi-random rotation
		p.xy *= rotate2d(a);
		p.xz *= rotate2d(a * 0.5);
		p.yz *= rotate2d(a + a);
		// pick semi-random axis for plane
		float c = mod(i, 3.) == 0. ? p.x : mod(i, 3.) == 1. ? p.y : p.z;
		// subtract plane from object, using onioning and offset
		// to give the plane thickness and move it away from the centre
		c = abs(c - o) - s;
		d = max(d, -c);
	}
	return d; // return final sdf value
}

// scene/map function
float time = 0.;
vec4 subsurfaceColor = vec4(0, 0, 0, 1);
vec4 glowColor = vec4(0, 0, 0, 0);
float objectTransmission = 0.;
float indexOfRefraction = 0.;
float mp(vec3 p)
{
    // rotate entire scene slowly
	p.xz *= rotate2d(time * 0.03 + 1.);
	p.yz *= rotate2d(time * 0.05 + 0.5);
    // create 2 boxes - one is the actual object, one is purely used for my fake subsurface
	float d = boxSDF(p, vec3(objectSize)) - 0.1;
	float c = boxSDF(p, vec3(subsurfaceSize));
    // shatter box
	d = sh(p, d, sin(time * 0.01 + 0.3) * 3., (cos(time * 0.1) * 0.5 + 0.5) * 0.5 + 0.008, 0.4);
    // set scene distance, add glow
	float sceneDistance = d;
	glowColor.rgb += 0.001 / (0.001 + d*d) * normalize(p*p) * 0.008;
    // set object values - doing inside the scene allows for easier and nicer effects!
	if (sceneDistance < 0.001) {
		subsurfaceColor = pow(c, 3.) * baseSubsurfaceColor; // fake subsurface
		indexOfRefraction = 1.5 + c * 0.1; // index of refraction
		objectTransmission = 0.8 - c * 0.2; // object transmission
	}
	return sceneDistance; // return the distance - this is only used for the normals function
}

// inlined raymarcher. Mostly standard, but multiplies the scene distance by the inversion factor
void tr(vec3 rayOrigin, vec3 originalRayDirection, float transparencyInversion, out float currentDistance)
{
    currentDistance = 0.;
    for (float i = 0.; i < 222.; i++) {
        float sceneDistance = mp(rayOrigin + originalRayDirection * currentDistance);
        sceneDistance *= transparencyInversion;
        currentDistance += sceneDistance;
        if (sceneDistance < 0.00005 || currentDistance > 16.)
            break;
    }
}

// inlined normal calculation. Using this mat3 is actually slightly less compact, but much cleaner
vec3 nm(vec3 currentRayPosition)
{
    mat3 k = mat3(currentRayPosition, currentRayPosition, currentRayPosition) - mat3(.0001);
    return normalize(mp(currentRayPosition) - vec3(mp(k[0]), mp(k[1]), mp(k[2])));
}

// pixel "shader" (coloury bits)
vec4 px(vec3 currentRayPosition, vec3 currentRayNormal, vec3 currentRayDirection, float currentDistance)
{
    vec4 currentColor;
    if (currentDistance > 16.) {
        currentColor = backgroundColor + length(currentRayDirection * currentRayDirection) * 0.2 + glowColor; // assign current color to background colour + glow
    } else {
        // axis-based diffuse lighting
        vec3 l = vec3(0.7, 0.4, 0.9);
        float df = length(currentRayNormal * l);
        // very basic fresnel effect and custom specular effect
        float fr = pow(1. - df, 2.) * 0.5;
        float sp = (1. - length(cross(currentRayDirection, currentRayNormal))) * 0.2;
        float ao = min(mp(currentRayPosition + currentRayNormal * 0.5) - 0.5, 0.3) * 0.3; // custom ambient occulusion effect
        currentColor = objectColor * (df + fr + subsurfaceColor) + fr + sp + ao + glowColor; // mix it all together
    }
    return currentColor;
}

void main()
{
    time = mod(TIME + 19., 120.); // keep time low to reduce issues

    vec2 uv = vec2(gl_FragCoord.x / RENDERSIZE.x, gl_FragCoord.y / RENDERSIZE.y);
    uv -= 0.5;
    uv /= vec2(RENDERSIZE.y / RENDERSIZE.x, 1);

    // ray origin and direction
    vec3 rayOrigin = vec3(0, 0 , -8);
    vec3 originalRayDirection = normalize(vec3(uv, 1));

    float currentTransmission = 1.;
    float transparencyInversion = 1.;
    vec4 finalColor = vec4(0);

    for (int i = 0; i < 6 * 2; i++) { // compacted transparency loop - depth 6 (2 pass per layer)
        float currentDistance;
        tr(rayOrigin, originalRayDirection, transparencyInversion, currentDistance);

        vec3 currentRayPosition = rayOrigin + originalRayDirection * currentDistance;
        vec3 currentRayNormal = nm(currentRayPosition);

        vec3 currentRayDirection = originalRayDirection;
        rayOrigin = currentRayPosition - currentRayNormal * (0.01 * transparencyInversion); // trace and calculate values
        originalRayDirection = refract(currentRayDirection, currentRayNormal * transparencyInversion, transparencyInversion > 0. ? 1. / indexOfRefraction : indexOfRefraction); // refract
        if (length(originalRayDirection) == 0.)
            originalRayDirection = reflect(currentRayDirection, currentRayNormal * transparencyInversion); // reflect if refraction failed

        vec4 currentColor = px(currentRayPosition, currentRayNormal, currentRayDirection, currentDistance);

        transparencyInversion *= -1.;
        if (transparencyInversion < 0.)
            finalColor = mix(finalColor, currentColor, currentTransmission); // get colour and mix it
        currentTransmission *= objectTransmission;
        if (currentTransmission <= 0. || currentDistance > 128.)
            break; // update trasmission and break if needed
    }

    gl_FragColor = finalColor; // output colour
}
