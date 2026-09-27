"""Small repeatable wave and macro-noise maps, authored through Blender."""
import bpy, math
ROOT='C:/Users/yusuk/Documents/GitHub/EternalEnigma'
OUT=ROOT+'/Assets/Art/EnvironmentKit/Textures/'
def lattice(x,y,res):
    h=((x%res)*374761393+(y%res)*668265263+7919)&0xffffffff
    h=((h^(h>>13))*1274126177)&0xffffffff
    return ((h^(h>>16))&65535)/65535
def noise(x,y,res):
    x*=res;y*=res;ix=int(x);iy=int(y);a=x-ix;b=y-iy
    a=a*a*(3-2*a);b=b*b*(3-2*b)
    return (lattice(ix,iy,res)*(1-a)+lattice(ix+1,iy,res)*a)*(1-b)+(lattice(ix,iy+1,res)*(1-a)+lattice(ix+1,iy+1,res)*a)*b
for name in ['Ocean','OceanNoise']:
    im=bpy.data.images.get(name) or bpy.data.images.new(name,128,128,alpha=True);pixels=[]
    for y in range(128):
        for x in range(128):
            if name=='Ocean':
                wave=math.sin(y*math.tau/32+math.sin(x*math.tau/64)*.75)
                value=.80+.045*math.sin(y*math.tau/64)+(.12 if wave>.965 and (x//12+y//32)%3!=0 else 0)
            else:value=sum(noise(x/128,y/128,r)*w for r,w in [(2,.55),(4,.25),(8,.13),(16,.07)])
            pixels.extend((value,value,value,1))
    im.pixels=pixels;im.file_format='PNG';im.filepath_raw=OUT+name+'.png';im.save()
bpy.ops.wm.save_as_mainfile(filepath=ROOT+'/ArtSource/Environment/EnvironmentKit.blend')
print('Saved 128x128 wave and tileable macro-noise textures.')
