import urllib.request, time, os
lat0,lat1,lon0,lon1=-25.165,-25.030,-50.220,-50.080
S=0.015
la=lat0; i=0; fails=[]
while la<lat1-1e-9:
    lo=lon0
    while lo<lon1-1e-9:
        f=f'{la:.3f}_{lo:.3f}.xml'
        if not os.path.exists(f) or os.path.getsize(f)<200:
            url=f'https://api.openstreetmap.org/api/0.6/map?bbox={lo:.4f},{la:.4f},{min(lo+S,lon1):.4f},{min(la+S,lat1):.4f}'
            for t in range(4):
                try:
                    d=urllib.request.urlopen(urllib.request.Request(url,headers={'User-Agent':'CarretaJoyMapBuilder/1.0'}),timeout=180).read()
                    open(f,'wb').write(d); break
                except Exception as e:
                    print('erro',f,e); time.sleep(5*(t+1))
            else: fails.append(f)
            time.sleep(1)
        i+=1; lo+=S
    la+=S
print('tiles',i,'falhas',fails)
