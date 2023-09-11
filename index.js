const timeDisplay = document.querySelector('.display__time-left');
const countDownTitleDisplay = document.querySelector('#countdownTitle');
const beepSelector = document.getElementById('beepSelect');
const audio = document.getElementById('audio');
var canPlay = true;
var isPaused = false;
var then = Date.now();
var delay = 0;
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