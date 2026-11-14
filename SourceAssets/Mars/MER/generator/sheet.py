import sys, os
from PIL import Image, ImageDraw
HERE=os.path.dirname(os.path.abspath(__file__))
R=os.path.join(HERE,'out','renders')
def sheet(names, out, cols=3, w=480, h=360, refs=None):
    items=[]
    for n in names:
        if refs and n in refs:
            items.append(Image.open(refs[n]).convert('RGB'))
        items.append(Image.open(os.path.join(R,n+'.png')).convert('RGB'))
    rows=(len(items)+cols-1)//cols
    S=Image.new('RGB',(cols*w,rows*h),(230,230,232))
    for i,im in enumerate(items):
        im.thumbnail((w,h)); x=(i%cols)*w+(w-im.width)//2; y=(i//cols)*h+(h-im.height)//2; S.paste(im,(x,y))
    S.save(out); print(out, S.size)
REF=os.path.join(HERE,'..','reference')
refs={'front':REF+'/01_frente.png','left':REF+'/02_lado_esquerdo.png','rear':REF+'/03_tras.png','right':REF+'/04_lado_direito.png',
'top':REF+'/05_topo.png','q_front_l':REF+'/06_tres_quartos_frente.png','q_rear_r':REF+'/07_tres_quartos_tras.png','bottom':REF+'/08_baixo.png',
'under_side':REF+'/09_baixo_lateral.png','q_front_lo':REF+'/10_frente_baixa.png'}
mode=sys.argv[1] if len(sys.argv)>1 else 'cmp'
if mode=='cmp':
    sheet(['front','left','rear','right','top'],os.path.join(HERE,'out')+'/compare_1.png',cols=2,refs=refs)
    sheet(['q_front_l','q_rear_r','bottom','under_side','q_front_lo'],os.path.join(HERE,'out')+'/compare_2.png',cols=2,refs=refs)
    sheet(['head','arm','wheel','hga'],os.path.join(HERE,'out')+'/closeups.png',cols=2,w=640,h=480)
