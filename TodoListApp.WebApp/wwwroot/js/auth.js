// дрібна взаємодія: легкий паралакс на фоні
(() => {
    const g = document.querySelector('.gradient');
    if(!g) return;
    let t = 0;
    const loop = () => {
        t += 0.004;
        g.style.transform = `translateY(${Math.sin(t)*10}px)`;
        requestAnimationFrame(loop);
    };
    loop();
})();
