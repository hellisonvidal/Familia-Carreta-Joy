import math, urllib.request, io, numpy as np
from PIL import Image
Z=14
def tile(lat,lon):
    n=2**Z; x=(lon+180)/360*n; y=(1-math.asinh(math.tan(math.radians(lat)))/math.pi)/2*n; return x,y
lat0,lat1,lon0,lon1=-25.165,-25.030,-50.220,-50.080
x0,y0=tile(lat1,lon0); x1,y1=tile(lat0,lon1)
tx=range(int(x0),int(x1)+1); ty=range(int(y0),int(y1)+1)
big=np.zeros((len(ty)*256,len(tx)*256))
for j,yy in enumerate(ty):
    for i,xx in enumerate(tx):
        d=urllib.request.urlopen(f'https://s3.amazonaws.com/elevation-tiles-prod/terrarium/{Z}/{xx}/{yy}.png').read()
        a=np.asarray(Image.open(io.BytesIO(d)).convert('RGB')).astype(float)
        big[j*256:(j+1)*256,i*256:(i+1)*256]=a[...,0]*256+a[...,1]+a[...,2]/256-32768
# recorta a área exata
px0=(x0-int(x0))*256; py0=(y0-int(y0))*256; px1=(x1-int(x0))*256; py1=(y1-int(y0))*256
crop=big[int(py0):int(py1),int(px0):int(px1)]
np.save('dem_full.npy',crop); print(crop.shape, crop.min(), crop.max())
