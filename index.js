const timeDisplay = document.querySelector('.display__time-left');
const countDownTitleDisplay = document.querySelector('#countdownTitle');
const deathCountDisplay = document.querySelector('#deathCount');
const beepSelector = document.getElementById('beepSelect');
const audio = document.getElementById('audio');
var canPlay = true;
var isPaused = false;
var then = Date.now();
var delay = 0;
var deathCount = 0;

document.addEventListener('keydown', (event) => {
    const target = event.target;
    const isEditable = target.isContentEditable || ['INPUT', 'TEXTAREA', 'SELECT'].includes(target.tagName);
    if (isEditable) {
        return;
    }

    if (event.key === '+' || event.key === '=') {
        event.preventDefault();
        UpdateDeathCount(1);
        return;
    }
    if (event.key === '-' || event.key === '_') {
        event.preventDefault();
        UpdateDeathCount(-1);
        return;
    }
    if (event.key === '0') {
        event.preventDefault();
        ResetDeathCount();
    }
});
var countdown = setInterval(() => {
    if (!isPaused) {
        then += delay * 1000;
        console.log(then);
        const secondsLeft = Math.round((then - Date.now()) / 1000);
        delay = 0;
        if (secondsLeft <= 0) {
            then = Date.now();
            timeDisplay.textContent = '00:00:00';
            if (canPlay) {
                audio.play();
                canPlay = false;
                tempSecondsLeft = null;
            }
            return;
        }
        displayTimeLeft(secondsLeft);
    }
    else {
        delay++;
    }
}, 1000)

function displayTimeLeft(seconds) {
    const hours = Math.floor(seconds / 3600);
    const minutes = (Math.floor(seconds / 60)) % 60;
    const reminderSeconds = seconds % 60;
    const display = `${hours < 10 ? '0' : ''}${hours}:${minutes < 10 ? '0' : ''}${minutes}:${reminderSeconds < 10 ? '0' : ''}${reminderSeconds}`;
    timeDisplay.textContent = display;
}

function IncreaseTime(number) {
    then += number * 1000 * 60
    if (Math.round((then - Date.now()) / 1000) < 0 && number < 0) {
        canPlay = false;
    }
    canPlay = true;
}

function Reset() {
    then = Date.now();
    timeDisplay.textContent = '00:00:00';
    canPlay = false;
}

function UpdateTitle(text) {
    countDownTitleDisplay.textContent = text;
}

function changeBeep() {
    audio.src = `beeps/${beepSelector.value}`;
}

function SetPause() {
    isPaused = !isPaused;
    let btn = document.getElementById('pause-btn');
    btn.innerText = isPaused ? 'Resume' : 'Pause';
}

function UpdateDeathCount(delta) {
    deathCount = Math.max(0, deathCount + delta);
    deathCountDisplay.textContent = deathCount;
}

function ResetDeathCount() {
    deathCount = 0;
    deathCountDisplay.textContent = deathCount;
}
