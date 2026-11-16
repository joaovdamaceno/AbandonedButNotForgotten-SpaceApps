using UnityEngine;
using UnityEngine.UIElements;
namespace Abandoned.SpaceApps.Opportunity
{
    /// <summary>
    /// Prompt único de "segurar E" do módulo Spirit and Opportunity: a tecla E desenhada como botão de teclado, uma barra com o
    /// progresso REAL da ação e uma confirmação visual quando ela termina. Só apresenta: quem mede o progresso é a própria
    /// interação (observação, RAT, foto), então a barra nunca anda por conta própria.
    /// </summary>
    public sealed class HoldPrompt
    {
        /// <summary>Quanto tempo (tempo real) a confirmação de conclusão fica na tela.</summary>
        public const float DoneSeconds=1.1f;
        public enum Placement { Corner, Hint }
        const string Idle="hold-prompt--idle",Holding="hold-prompt--holding",Waiting="hold-prompt--waiting",Done="hold-prompt--done";
        readonly VisualElement root,fill;
        readonly Label holdWord,text;
        float doneRemaining;
        string doneText,shownText,shownMode,shownWord;
        int shownPermille=-1;
        bool shown,shownKnown,placed;
        Placement placement;
        public VisualElement Root=>root;
        public bool Visible=>shown;
        /// <summary>A confirmação de conclusão está na tela agora.</summary>
        public bool ShowingDone=>doneRemaining>0;
        public float ShownProgress=>shownPermille<0?0:shownPermille/1000f;

        public HoldPrompt()
        {
            root=Box(null,"hold-prompt");root.name="hold-prompt";
            MakeLabel(root,"hold-key").text="E";
            var body=Box(root,"hold-body");
            var line=Box(body,"hold-line");
            var check=Box(line,"hold-check");Box(check,"hold-check-mark");
            holdWord=MakeLabel(line,"hold-word");text=MakeLabel(line,"hold-text");
            fill=Box(Box(body,"hold-track"),"hold-fill");
            SetShown(false);
        }

        /// <summary>
        /// Onde o prompt fica: Hint = dentro da pilha de dicas do alto à direita (no lugar delas); Corner = canto inferior esquerdo,
        /// logo depois da pilha na hierarquia (acima das telas de estudo, abaixo do que vem depois).
        /// </summary>
        public void Place(Placement where,VisualElement hintStack)
        {
            if(hintStack==null)return;
            if(placed&&placement==where&&root.parent!=null)return;
            placed=true;placement=where;
            root.RemoveFromHierarchy();
            root.EnableInClassList("hold-prompt--hint",where==Placement.Hint);
            root.EnableInClassList("hold-prompt--corner",where==Placement.Corner);
            if(where==Placement.Hint)hintStack.Insert(0,root);
            else hintStack.parent.Insert(hintStack.parent.IndexOf(hintStack)+1,root);
        }

        /// <summary>Marca a ação como concluída: mostra a confirmação verde por <see cref="DoneSeconds"/>, mesmo que a tela da ação já tenha fechado.</summary>
        public void Complete(string label){doneText=label;doneRemaining=DoneSeconds;}

        /// <summary>Esconde já, sem confirmação (pausa, reinício, troca de capítulo).</summary>
        public void Cancel(){doneRemaining=0;SetShown(false);}

        /// <summary>
        /// Atualiza o prompt. <paramref name="progress"/> é a fração real da ação (0–1); <paramref name="holding"/> é a tecla
        /// pressionada; <paramref name="waiting"/> indica que a ação ainda não pode avançar (instrumento se preparando).
        /// </summary>
        public void Apply(bool active,string label,float progress,bool holding,bool waiting,float unscaledDeltaSeconds)
        {
            if(doneRemaining>0)
            {
                doneRemaining-=Mathf.Max(0,unscaledDeltaSeconds);
                // Uma nova ação já está avançando: o progresso dela toma o lugar da confirmação anterior.
                if(active&&!waiting&&progress>.02f)doneRemaining=0;
            }
            bool done=doneRemaining>0;
            SetShown(active||done);
            if(!shown)return;
            string mode=done?Done:waiting?Waiting:holding?Holding:Idle;
            if(mode!=shownMode)
            {
                foreach(var m in new[]{Idle,Holding,Waiting,Done})root.EnableInClassList(m,m==mode);
                shownMode=mode;
            }
            string word=Localization.Get("cmd.hold"),shownLabel=done?doneText:label;
            if(word!=shownWord){holdWord.text=word;shownWord=word;}
            if(shownLabel!=shownText){text.text=shownLabel;shownText=shownLabel;}
            float p=done?1:Mathf.Clamp01(progress);
            int permille=Mathf.RoundToInt(p*1000);
            if(permille!=shownPermille){fill.style.width=Length.Percent(permille/10f);shownPermille=permille;}
        }

        void SetShown(bool visible)
        {
            if(shownKnown&&shown==visible)return;
            shownKnown=true;shown=visible;
            root.style.display=visible?DisplayStyle.Flex:DisplayStyle.None;
        }

        static VisualElement Box(VisualElement parent,string cls)
        {
            var e=new VisualElement();e.AddToClassList(cls);e.pickingMode=PickingMode.Ignore;
            if(parent!=null)parent.Add(e);
            return e;
        }
        static Label MakeLabel(VisualElement parent,string cls)
        {
            var l=new Label();l.AddToClassList(cls);l.pickingMode=PickingMode.Ignore;parent.Add(l);return l;
        }
    }
}
